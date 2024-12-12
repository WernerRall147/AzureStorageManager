using Azure.Core;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using AzureStorageManager.Models;
using AzureStorageManager.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AzureStorageManager.Services
{
    public class FileShareService
    {
        private readonly ShareServiceClient _shareServiceClient;
        private readonly ShareClient _shareClient;

        /// <summary>
        /// Constructor without custom ShareClientOptions.
        /// </summary>
        public FileShareService(string serviceUri, string fileShareName, TokenCredential credential)
        {
            _shareServiceClient = new ShareServiceClient(new Uri(serviceUri), credential);
            _shareClient = _shareServiceClient.GetShareClient(fileShareName);
        }

        /// <summary>
        /// Constructor that accepts ShareClientOptions, allowing custom pipeline policies.
        /// </summary>
        public FileShareService(string serviceUri, string fileShareName, TokenCredential credential, ShareClientOptions options)
        {
            _shareServiceClient = new ShareServiceClient(new Uri(serviceUri), credential, options);
            _shareClient = _shareServiceClient.GetShareClient(fileShareName);
        }

        /// <summary>
        /// Lists files in the given file share directory, compares their MD5 hashes with local files,
        /// updates metadata if there is a mismatch, and exports both a full report and a mismatches-only report.
        /// </summary>
        /// <param name="localDirectory">Path to the local directory containing files to compare against.</param>
        /// <param name="csvFileName">Originally passed CSV file name (used for directory reference).</param>
        public async Task ListAndVerifyFilesAsync(string localDirectory, string csvFileName)
        {
            var fileMetadataList = new List<FileMetadata>();

            try
            {
                var rootDirectoryClient = _shareClient.GetRootDirectoryClient();

                await foreach (var fileItem in rootDirectoryClient.GetFilesAndDirectoriesAsync())
                {
                    if (!fileItem.IsDirectory)
                    {
                        var fileClient = rootDirectoryClient.GetFileClient(fileItem.Name);

                        // Fetch file properties and retrieve the remote MD5 hash from metadata
                        var fileProperties = await fileClient.GetPropertiesAsync();
                        fileProperties.Value.Metadata.TryGetValue("md5", out string? remoteMD5);

                        // Compute the local MD5 hash from the corresponding local file
                        string localFilePath = Path.Combine(localDirectory, fileItem.Name);

                        // Check if the local file exists
                        if (!File.Exists(localFilePath))
                        {
                            fileMetadataList.Add(new FileMetadata(
                                fileItem.Name,
                                "",              // localHash
                                remoteMD5 ?? "",
                                "LocalFileMissing"
                            ));
                            continue;
                        }

                        string localMD5 = FileHashUtility.CalculateMD5(localFilePath);

                        // Only set the metadata if remoteMD5 doesn't exist or is empty
                        if (string.IsNullOrEmpty(remoteMD5))
                        {
                            await fileClient.SetMetadataAsync(new Dictionary<string, string>
                            {
                                { "md5", localMD5 }
                            });

                            // Re-fetch the properties to confirm the update (optional)
                            var updatedProperties = await fileClient.GetPropertiesAsync();
                            updatedProperties.Value.Metadata.TryGetValue("md5", out remoteMD5);
                        }

                        // Determine status
                        string status = (remoteMD5 == localMD5) ? "Match" : "Mismatch";
                        fileMetadataList.Add(new FileMetadata(
                            fileItem.Name,
                            localMD5,
                            remoteMD5 ?? "",
                            status
                        ));
                    }
                }

                // Generate timestamp for report filenames
                string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                string directoryPath = Path.GetDirectoryName(csvFileName) ?? Directory.GetCurrentDirectory();

                // Create full report
                string fullReportFileName = Path.Combine(directoryPath, $"FullReport_{timestamp}.csv");
                CsvExporter.ExportToCsv(fileMetadataList, fullReportFileName);

                // Create mismatches-only report
                var mismatchesOnly = fileMetadataList
                    .Where(f => !string.Equals(f.Status, "Match", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                string mismatchesReportFileName = Path.Combine(directoryPath, $"MismatchesOnlyReport_{timestamp}.csv");
                CsvExporter.ExportToCsv(mismatchesOnly, mismatchesReportFileName);

                Console.WriteLine("Verification complete.");
                Console.WriteLine($"Full report saved to {fullReportFileName}");
                Console.WriteLine($"Mismatches-only report saved to {mismatchesReportFileName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while processing files: {ex.Message}");
            }
        }
    }
}

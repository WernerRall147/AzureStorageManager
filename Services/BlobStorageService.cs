using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using AzureStorageManager.Models;
using AzureStorageManager.Utilities;

namespace AzureStorageManager.Services
{
    public class BlobStorageService
    {
        private readonly BlobContainerClient _containerClient;

        public BlobStorageService(BlobServiceClient blobServiceClient, string containerName)
        {
            _containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        }

        public async Task ListAndVerifyBlobsAsync(string localDirectory, string csvFileName)
        {
            var fileMetadataList = new List<FileMetadata>();

            await foreach (var blobItem in _containerClient.GetBlobsAsync())
            {
                var blobClient = _containerClient.GetBlobClient(blobItem.Name);

                // Get blob properties and metadata
                var blobProperties = await blobClient.GetPropertiesAsync();
                blobProperties.Value.Metadata.TryGetValue("md5", out string? remoteMD5);

                string localFilePath = Path.Combine(localDirectory, blobItem.Name);

                // Check if local file exists
                if (!File.Exists(localFilePath))
                {
                    fileMetadataList.Add(new FileMetadata(
                        blobItem.Name,
                        "",                 // localHash
                        remoteMD5 ?? "",
                        "LocalFileMissing"
                    ));
                    continue;
                }

                string localMD5 = FileHashUtility.CalculateMD5(localFilePath);

                // Only set the metadata if remoteMD5 doesn't exist or is empty
                if (string.IsNullOrEmpty(remoteMD5))
                {
                    await blobClient.SetMetadataAsync(new Dictionary<string, string>
                    {
                        { "md5", localMD5 }
                    });

                    // Re-fetch properties to confirm update (optional)
                    var updatedProperties = await blobClient.GetPropertiesAsync();
                    updatedProperties.Value.Metadata.TryGetValue("md5", out remoteMD5);
                }
                
                // Determine status
                string status = (remoteMD5 == localMD5) ? "Match" : "Mismatch";
                fileMetadataList.Add(new FileMetadata(
                    blobItem.Name,
                    localMD5,
                    remoteMD5 ?? "",
                    status
                ));
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
    }
}

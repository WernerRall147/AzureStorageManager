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
        }        /// <summary>
        /// Lists files in the given file share directory, compares their MD5 hashes with local files,
        /// updates metadata if there is a mismatch, and exports both a full report and a mismatches-only report.
        /// </summary>
        /// <param name="localDirectory">Path to the local directory containing files to compare against.</param>
        /// <param name="csvFileName">Originally passed CSV file name (used for directory reference).</param>
        public async Task ListAndVerifyFilesAsync(string localDirectory, string csvFileName)
        {
            var fileMetadataList = new List<FileMetadata>();
            var processedLocalFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // To track which local files we've processed
            var azureFilesDict = new Dictionary<string, (string hash, string path, long size)>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var rootDirectoryClient = _shareClient.GetRootDirectoryClient();

                // First collect all Azure files into a dictionary for efficient lookup
                await foreach (var fileItem in rootDirectoryClient.GetFilesAndDirectoriesAsync())
                {
                    if (!fileItem.IsDirectory)
                    {
                        var fileClient = rootDirectoryClient.GetFileClient(fileItem.Name);

                        // Fetch file properties and retrieve the remote MD5 hash from metadata
                        var fileProperties = await fileClient.GetPropertiesAsync();
                        fileProperties.Value.Metadata.TryGetValue("md5", out string? remoteMD5);
                        string remotePath = fileClient.Uri.ToString();
                        long fileSize = fileProperties.Value.ContentLength;

                        // Store in dictionary for lookup
                        azureFilesDict[fileItem.Name] = (remoteMD5 ?? "", remotePath, fileSize);
                    }
                }                // First collect all Azure files into a dictionary for efficient lookup
                await foreach (var fileItem in rootDirectoryClient.GetFilesAndDirectoriesAsync())
                {
                    if (!fileItem.IsDirectory)
                    {
                        var fileClient = rootDirectoryClient.GetFileClient(fileItem.Name);

                        // Fetch file properties and retrieve the remote MD5 hash from metadata
                        var fileProperties = await fileClient.GetPropertiesAsync();
                        fileProperties.Value.Metadata.TryGetValue("md5", out string? remoteMD5);
                        string remotePath = fileClient.Uri.ToString();
                        long fileSize = fileProperties.Value.ContentLength;

                        // Store in dictionary for lookup
                        azureFilesDict[fileItem.Name] = (remoteMD5 ?? "", remotePath, fileSize);
                    }
                }

                // Now process local files and match with Azure files
                if (Directory.Exists(localDirectory))
                {
                    try
                    {
                        // Get all files in the local directory
                        var localFiles = Directory.GetFiles(localDirectory);
                        
                        foreach (var localFilePath in localFiles)
                        {
                            // Get just the file name for comparison
                            string fileName = Path.GetFileName(localFilePath);
                            string localMD5 = FileHashUtility.CalculateMD5(localFilePath);
                            long localFileSize = new FileInfo(localFilePath).Length;
                            
                            // Check if this file exists in Azure
                            if (azureFilesDict.TryGetValue(fileName, out var azureInfo))
                            {
                                string status = (azureInfo.hash == localMD5) ? "Match" : "Mismatch";
                                
                                // Add file with both local and Azure information
                                fileMetadataList.Add(new FileMetadata(
                                    fileName,
                                    localMD5,
                                    azureInfo.hash,
                                    status,
                                    localFilePath,
                                    azureInfo.path,
                                    localFileSize
                                ));
                                
                                // If the Azure file doesn't have MD5 metadata, set it
                                if (string.IsNullOrEmpty(azureInfo.hash))
                                {
                                    var fileClient = rootDirectoryClient.GetFileClient(fileName);
                                    await fileClient.SetMetadataAsync(new Dictionary<string, string>
                                    {
                                        { "md5", localMD5 }
                                    });
                                }
                                
                                // Mark as processed
                                processedLocalFiles.Add(fileName);
                                
                                // Remove from Azure dictionary to track what's been processed
                                azureFilesDict.Remove(fileName);
                            }
                            else
                            {
                                // Local-only file
                                fileMetadataList.Add(new FileMetadata(
                                    fileName,
                                    localMD5,
                                    "", // No remote hash since file doesn't exist in Azure
                                    "AzureFileMissing", // Status indicating file is missing in Azure
                                    localFilePath,
                                    "", // No remote path since file doesn't exist in Azure
                                    localFileSize
                                ));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning: Error scanning local directory: {ex.Message}");
                    }
                }
                
                // Add remaining Azure-only files
                foreach (var item in azureFilesDict)
                {
                    fileMetadataList.Add(new FileMetadata(
                        item.Key,
                        "", // No local hash
                        item.Value.hash,
                        "LocalFileMissing",
                        "", // No local path
                        item.Value.path,
                        item.Value.size
                    ));
                }                // Add remaining Azure-only files
                foreach (var item in azureFilesDict)
                {
                    fileMetadataList.Add(new FileMetadata(
                        item.Key,
                        "", // No local hash
                        item.Value.hash,
                        "LocalFileMissing",
                        "", // No local path
                        item.Value.path,
                        item.Value.size
                    ));
                }

                // Export the results to CSV
                CsvExporter.ExportToCsv(fileMetadataList, csvFileName);
                Console.WriteLine($"Report saved to: {csvFileName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while processing files: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Copies files from the local directory to the file share, verifies their MD5 hashes,
        /// updates metadata if there is a mismatch, and exports a report.
        /// </summary>
        /// <param name="localDirectory">Path to the local directory containing files to copy and verify.</param>
        /// <param name="csvFileName">Path to the CSV file where the report will be saved.</param>
        public async Task CopyAndVerifyFilesAsync(string localDirectory, string csvFileName)
        {
            var fileMetadataList = new List<FileMetadata>();
            var rootDirectoryClient = _shareClient.GetRootDirectoryClient();

            foreach (var filePath in Directory.GetFiles(localDirectory))
            {
                string fileName = Path.GetFileName(filePath);
                var fileClient = rootDirectoryClient.GetFileClient(fileName);

                Console.WriteLine($"Uploading file: {fileName}");
                using (var fileStream = File.OpenRead(filePath))
                {
                    await fileClient.CreateAsync(fileStream.Length);
                    using (var memoryStream = new MemoryStream())
                    {
                        fileStream.CopyTo(memoryStream);
                        memoryStream.Position = 0;
                        await fileClient.UploadAsync(memoryStream);
                    }
                }                string localHash = FileHashUtility.CalculateMD5(filePath);
                var fileProperties = await fileClient.GetPropertiesAsync();
                fileProperties.Value.Metadata.TryGetValue("md5", out string? fileHash);

                if (string.IsNullOrEmpty(fileHash) || fileHash != localHash)
                {
                    Console.WriteLine($"Updating MD5 metadata for file: {fileName}");
                    await fileClient.SetMetadataAsync(new Dictionary<string, string> { { "md5", localHash } });
                }

                string safeHash = fileHash ?? string.Empty;
                fileMetadataList.Add(new FileMetadata(fileName, localHash, safeHash, "StatusPlaceholder"));
            }

            CsvExporter.ExportToCsv(fileMetadataList, csvFileName);
        }
    }
}

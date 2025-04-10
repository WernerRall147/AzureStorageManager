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
        }        public async Task ListAndVerifyBlobsAsync(string localDirectory, string csvFileName)
        {
            var fileMetadataList = new List<FileMetadata>();
            var processedLocalFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // To track which local files we've processed
            var azureFilesDict = new Dictionary<string, (string hash, string path, long size)>(StringComparer.OrdinalIgnoreCase);

            // First collect all Azure blob files into a dictionary for efficient lookup
            await foreach (var blobItem in _containerClient.GetBlobsAsync())
            {
                var blobClient = _containerClient.GetBlobClient(blobItem.Name);

                // Get blob properties and metadata
                var blobProperties = await blobClient.GetPropertiesAsync();
                blobProperties.Value.Metadata.TryGetValue("md5", out string? remoteMD5);
                string remotePath = blobClient.Uri.ToString();
                long fileSize = blobItem.Properties.ContentLength ?? 0; // Use default 0 if null

                // Store in dictionary for lookup
                azureFilesDict[blobItem.Name] = (remoteMD5 ?? "", remotePath, fileSize);
            }

            // Now process local files and match with Azure files
            if (Directory.Exists(localDirectory))
            {
                try
                {
                    // Get all files in the local directory (including subdirectories)
                    var localFiles = Directory.GetFiles(localDirectory, "*", SearchOption.AllDirectories);
                    
                    foreach (var localFilePath in localFiles)
                    {
                        // Get the relative path from the local directory
                        string relativePath = Path.GetRelativePath(localDirectory, localFilePath);
                        string localMD5 = FileHashUtility.CalculateMD5(localFilePath);
                        long localFileSize = new FileInfo(localFilePath).Length;
                        
                        // Check if this file exists in Azure
                        if (azureFilesDict.TryGetValue(relativePath, out var azureInfo))
                        {
                            string status = (azureInfo.hash == localMD5) ? "Match" : "Mismatch";
                            
                            // Add file with both local and Azure information
                            fileMetadataList.Add(new FileMetadata(
                                relativePath,
                                localMD5,
                                azureInfo.hash,
                                status,
                                localFilePath,
                                azureInfo.path,
                                localFileSize
                            ));
                            
                            // Mark as processed
                            processedLocalFiles.Add(relativePath);
                            
                            // Remove from Azure dictionary to track what's been processed
                            azureFilesDict.Remove(relativePath);
                        }
                        else
                        {
                            // Local-only file
                            fileMetadataList.Add(new FileMetadata(
                                relativePath,
                                localMD5,
                                "", // No remote hash since file doesn't exist in Azure
                                "AzureBlobMissing", // Status indicating file is missing in Azure
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
            }
            
            // Generate timestamp for report filename
            string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            string directoryPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop); // Save to Desktop for easier access
            
            // Create a single comprehensive report with a clearer name
            string reportFileName = Path.Combine(directoryPath, $"AzureStorageReport_{timestamp}.csv");
            CsvExporter.ExportToCsv(fileMetadataList, reportFileName);

            // Print colored success message
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Verification complete.");
            Console.WriteLine($"Comprehensive report saved to: {reportFileName}");
            Console.ResetColor();
        }

        public async Task CopyAndVerifyBlobsAsync(string localDirectory, string csvFileName)
        {
            var fileMetadataList = new List<FileMetadata>();

            foreach (var filePath in Directory.GetFiles(localDirectory))
            {
                string fileName = Path.GetFileName(filePath);
                var blobClient = _containerClient.GetBlobClient(fileName);

                Console.WriteLine($"Uploading file: {fileName}");
                await blobClient.UploadAsync(filePath, true);                string localHash = FileHashUtility.CalculateMD5(filePath);
                var blobProperties = await blobClient.GetPropertiesAsync();
                blobProperties.Value.Metadata.TryGetValue("md5", out string? blobHash);

                if (string.IsNullOrEmpty(blobHash) || blobHash != localHash)
                {
                    Console.WriteLine($"Updating MD5 metadata for blob: {fileName}");
                    await blobClient.SetMetadataAsync(new Dictionary<string, string> { { "md5", localHash } });
                }

                string safeHash = blobHash ?? string.Empty;
                fileMetadataList.Add(new FileMetadata(fileName, localHash, safeHash, "StatusPlaceholder"));
            }

            CsvExporter.ExportToCsv(fileMetadataList, csvFileName);
        }
    }
}

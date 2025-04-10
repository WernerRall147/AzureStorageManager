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

            // First process all Azure blob files
            await foreach (var blobItem in _containerClient.GetBlobsAsync())
            {var blobClient = _containerClient.GetBlobClient(blobItem.Name);

                // Get blob properties and metadata
                var blobProperties = await blobClient.GetPropertiesAsync();
                blobProperties.Value.Metadata.TryGetValue("md5", out string? remoteMD5);                string localFilePath = Path.Combine(localDirectory, blobItem.Name);
                string remotePath = blobClient.Uri.ToString();
                long fileSize = blobItem.Properties.ContentLength ?? 0; // Use default 0 if null

                // Check if local file exists
                if (!File.Exists(localFilePath))
                {
                    fileMetadataList.Add(new FileMetadata(
                        blobItem.Name,
                        "",                 // localHash
                        remoteMD5 ?? "",
                        "LocalFileMissing",
                        "",                 // localPath (empty since file doesn't exist)
                        remotePath,
                        fileSize
                    ));
                    continue;
                }

                string localMD5 = FileHashUtility.CalculateMD5(localFilePath);
                long localFileSize = new FileInfo(localFilePath).Length;

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
                    status,
                    localFilePath,
                    remotePath,
                    localFileSize                ));
                
                // Keep track of processed local files
                processedLocalFiles.Add(blobItem.Name);
            }
            
            // Now scan local directory to find files that don't exist in Azure
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
                        
                        // Skip if we've already processed this file (it exists in Azure)
                        if (processedLocalFiles.Contains(relativePath))
                            continue;
                        
                        // If we reach here, this is a local-only file
                        string localMD5 = FileHashUtility.CalculateMD5(localFilePath);
                        long localFileSize = new FileInfo(localFilePath).Length;
                        
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
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Error scanning local directory: {ex.Message}");
                }
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

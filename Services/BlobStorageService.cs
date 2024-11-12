using System.Collections.Generic;
using System.IO;
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

        public async Task ListAndVerifyBlobsAsync(string localDirectory)
        {
            var fileMetadataList = new List<FileMetadata>();

            await foreach (var blobItem in _containerClient.GetBlobsAsync())
            {
                Console.WriteLine($"Blob: {blobItem.Name}");

                var blobClient = _containerClient.GetBlobClient(blobItem.Name);
                string tempFilePath = Path.Combine(localDirectory, blobItem.Name);
                await blobClient.DownloadToAsync(tempFilePath);

                string localHash = FileHashUtility.CalculateMD5(tempFilePath);
                var blobProperties = await blobClient.GetPropertiesAsync();
                blobProperties.Value.Metadata.TryGetValue("md5", out string blobHash);

                Console.WriteLine($"Local MD5: {localHash}");
                Console.WriteLine($"Blob MD5 Metadata: {blobHash}");

                // Check if the remote hash matches; update only if it doesn't match or is missing
                if (blobHash == null || blobHash != localHash)
                {
                    Console.WriteLine($"Updating Blob MD5 metadata for: {blobItem.Name}");
                    await blobClient.SetMetadataAsync(new Dictionary<string, string> { { "md5", localHash } });

                    // Re-fetch the properties to confirm the metadata was set
                    blobProperties = await blobClient.GetPropertiesAsync();
                    blobProperties.Value.Metadata.TryGetValue("md5", out blobHash);
                }

                // Add to the report with the most recent metadata information
                fileMetadataList.Add(new FileMetadata(blobItem.Name, localHash, blobHash ?? ""));

               // File.Delete(tempFilePath);
            }

            CsvExporter.ExportToCsv(fileMetadataList, "BlobStorageReport.csv");
        }
    }
}

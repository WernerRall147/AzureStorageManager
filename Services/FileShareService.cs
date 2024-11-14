using System.Collections.Generic;
using System.IO;
using Azure.Storage.Files.Shares;
using AzureStorageManager.Models;
using AzureStorageManager.Utilities;

namespace AzureStorageManager.Services
{
    public class FileShareService
    {
        private readonly ShareClient _shareClient;

        public FileShareService(ShareClient shareClient)
        {
            _shareClient = shareClient;
        }

        public async Task ListAndVerifyFilesAsync(string localDirectory, string csvFileName)
        {
            var fileMetadataList = new List<FileMetadata>();

            await foreach (var fileItem in _shareClient.GetRootDirectoryClient().GetFilesAndDirectoriesAsync())
            {
                var fileClient = _shareClient.GetRootDirectoryClient().GetFileClient(fileItem.Name);
                string tempFilePath = Path.Combine(localDirectory, fileItem.Name);

                using (var downloadStream = (await fileClient.DownloadAsync()).Value.Content)
                using (var fileStream = File.Create(tempFilePath))
                {
                    await downloadStream.CopyToAsync(fileStream);
                }

                string localHash = FileHashUtility.CalculateMD5(tempFilePath);
                var fileProperties = await fileClient.GetPropertiesAsync();
                fileProperties.Value.Metadata.TryGetValue("md5", out string fileHash);

                Console.WriteLine($"Local MD5: {localHash}");
                Console.WriteLine($"File Share MD5 Metadata: {fileHash}");

                if (fileHash == null || fileHash != localHash)
                {
                    Console.WriteLine($"Updating File Share MD5 metadata for: {fileItem.Name}");
                    await fileClient.SetMetadataAsync(new Dictionary<string, string> { { "md5", localHash } });

                    // Re-fetch the properties to confirm the metadata was set
                    fileProperties = await fileClient.GetPropertiesAsync();
                    fileProperties.Value.Metadata.TryGetValue("md5", out fileHash);
                }

                fileMetadataList.Add(new FileMetadata(fileItem.Name, localHash, fileHash ?? ""));
            }

            CsvExporter.ExportToCsv(fileMetadataList, csvFileName);
        }
    }
}

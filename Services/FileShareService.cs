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

        public async Task ListAndVerifyFilesAsync(string localDirectory)
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

                if (fileHash == null || fileHash != localHash)
                {
                    await fileClient.SetMetadataAsync(new Dictionary<string, string> { { "md5", localHash } });
                }

                fileMetadataList.Add(new FileMetadata(fileItem.Name, localHash, fileHash ?? ""));

                File.Delete(tempFilePath);
            }

            CsvExporter.ExportToCsv(fileMetadataList, "FileShareReport.csv");
        }
    }
}

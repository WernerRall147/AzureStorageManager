using System.Collections.Generic;
using System.IO;
using System.Text;
using AzureStorageManager.Models;

namespace AzureStorageManager.Utilities
{    public static class CsvExporter
    {
        /// <summary>
        /// Properly formats and escapes a field for CSV output according to RFC 4180 standard
        /// </summary>
        /// <param name="field">The field value to format</param>
        /// <returns>Properly formatted CSV field</returns>
        private static string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return "\"\""; // Empty field as per CSV standard
                
            // If field contains commas, quotes, or newlines, it needs to be quoted
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
            {
                // Double up any quotes in the field
                field = field.Replace("\"", "\"\"");
                // Enclose in quotes
                return $"\"{field}\"";
            }
            
            return field;
        }
        
        public static void ExportToCsv(List<FileMetadata> fileMetadataList, string fileName)
        {
            // Define the full path for the CSV file
            var csvPath = Path.Combine(Directory.GetCurrentDirectory(), fileName);
            var csvContent = new StringBuilder();            // Write a more descriptive header line for the CSV file with clearer names
            csvContent.AppendLine("FileName,StorageLocation,LocalFilePath,AzureStoragePath,LocalMD5Hash,AzureMD5Hash,MatchStatus,FileSizeMB");

            // Write each file's details as a new line in the CSV
            foreach (var file in fileMetadataList)
            {                // Extract just the filename from full paths for cleaner reporting
                string simpleFileName = Path.GetFileName(file.FileName);
                
                // For Azure paths, try to make them shorter/cleaner when possible
                string remotePath = "";
                if (!string.IsNullOrEmpty(file.RemotePath))
                {
                    remotePath = file.RemotePath;
                    if (remotePath.Contains("blob.core.windows.net"))
                    {
                        // Extract just the container/path portion from the full Azure URL
                        int containerIndex = remotePath.IndexOf(".net/") + 5;
                        if (containerIndex > 5 && containerIndex < remotePath.Length)
                        {
                            remotePath = remotePath.Substring(containerIndex);
                        }
                    }
                }
                
                // Determine correct storage location based on both local and Azure existence
                string storageLocation;
                if (!string.IsNullOrEmpty(file.RemotePath) && !string.IsNullOrEmpty(file.LocalPath))
                    storageLocation = "Both"; // File exists in both places
                else if (!string.IsNullOrEmpty(file.RemotePath))
                    storageLocation = "Azure"; // File exists only in Azure
                else
                    storageLocation = "Local"; // File exists only locally
                
                // Convert bytes to MB with 3 decimal places
                double fileSizeMB = file.SizeInBytes / (1024.0 * 1024.0);
                string formattedSize = fileSizeMB.ToString("F3");
                
                // Format values for CSV standard - empty values are represented as empty quoted strings
                string localPathFormatted = EscapeCsvField(file.LocalPath ?? "");
                string remotePathFormatted = EscapeCsvField(remotePath);
                string localHashFormatted = EscapeCsvField(file.LocalHash ?? "");
                string remoteHashFormatted = EscapeCsvField(file.RemoteHash ?? "");
                string fileNameFormatted = EscapeCsvField(simpleFileName);
                
                csvContent.AppendLine($"{fileNameFormatted}," +
                                    $"{storageLocation}," + 
                                    $"{localPathFormatted}," +
                                    $"{remotePathFormatted}," +
                                    $"{localHashFormatted}," +
                                    $"{remoteHashFormatted}," +
                                    $"{file.Status}," +
                                    $"{formattedSize}");
            }

            // Save the CSV content to a file
            File.WriteAllText(csvPath, csvContent.ToString());
            
            // Use colored text for better visibility of completion
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"CSV report saved to: {csvPath}");
            Console.ResetColor();
        }
    }
}

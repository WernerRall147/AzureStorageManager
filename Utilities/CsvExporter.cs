using System.Collections.Generic;
using System.IO;
using System.Text;
using AzureStorageManager.Models;

namespace AzureStorageManager.Utilities
{
    public static class CsvExporter
    {
        public static void ExportToCsv(List<FileMetadata> fileMetadataList, string fileName)
        {
            // Define the full path for the CSV file
            var csvPath = Path.Combine(Directory.GetCurrentDirectory(), fileName);
            var csvContent = new StringBuilder();

            // Write the header line for the CSV file
            csvContent.AppendLine("FileName,LocalHash,RemoteHash,Status");

            // Write each file's details as a new line in the CSV
            foreach (var file in fileMetadataList)
            {
                csvContent.AppendLine($"{file.FileName},{file.LocalHash},{file.RemoteHash},{file.Status}");
            }

            // Save the CSV content to a file
            File.WriteAllText(csvPath, csvContent.ToString());
            Console.WriteLine($"CSV report saved to: {csvPath}");
        }
    }
}

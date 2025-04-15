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
using System.Net;

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
        /// Gets a list of directories and files at the specified path in the Azure File Share.
        /// </summary>
        /// <param name="directoryPath">The path to the directory to list (empty for root).</param>
        /// <returns>A list of file and directory items.</returns>
        public async Task<List<Models.ShareDirectoryItem>> ListDirectoryContentsAsync(string directoryPath = "")
        {
            List<Models.ShareDirectoryItem> items = new List<Models.ShareDirectoryItem>();
            
            try
            {
                // Get the directory client for the specified path
                ShareDirectoryClient directoryClient;
                if (string.IsNullOrEmpty(directoryPath))
                {
                    directoryClient = _shareClient.GetRootDirectoryClient();
                }
                else
                {
                    directoryClient = _shareClient.GetDirectoryClient(directoryPath);
                }

                // Check if the directory exists
                if (!await directoryClient.ExistsAsync())
                {
                    throw new DirectoryNotFoundException($"Directory '{directoryPath}' does not exist in share '{_shareClient.Name}'");
                }
                
                // List all files and directories in this directory
                await foreach (var item in directoryClient.GetFilesAndDirectoriesAsync())
                {
                    string itemPath = string.IsNullOrEmpty(directoryPath) 
                        ? item.Name 
                        : Path.Combine(directoryPath, item.Name).Replace("\\", "/");
                
                    if (item.IsDirectory)
                    {
                        items.Add(new Models.ShareDirectoryItem(
                            item.Name, 
                            itemPath, 
                            true, 
                            null, 
                            null));
                    }
                    else
                    {
                        // For files, get additional properties
                        var fileClient = directoryClient.GetFileClient(item.Name);
                        var properties = await fileClient.GetPropertiesAsync();
                        
                        items.Add(new Models.ShareDirectoryItem(
                            item.Name,
                            itemPath,
                            false,
                            properties.Value.ContentLength,
                            properties.Value.LastModified.DateTime));
                    }
                }
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Forbidden)
            {
                // Handle authorization errors with a more user-friendly message
                string storageAccount = _shareServiceClient.Uri.Host.Split('.')[0];
                throw new UnauthorizedAccessException(
                    $"Authorization Error: You don't have permission to list directories in the file share.\n\n" +
                    $"Please ensure your account has been granted the 'Storage File Data SMB Share Reader' " +
                    $"or 'Storage File Data SMB Share Contributor' role on the '{storageAccount}' storage account.", ex);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error listing directory contents: {ex.Message}");
                throw;
            }
            
            return items;
        }
        
        /// <summary>
        /// Displays the directory structure in a tree-like format for user selection.
        /// </summary>
        /// <param name="currentPath">Current directory path to display</param>
        /// <returns>A tuple with the list of items and the parent directory path</returns>
        public async Task<(List<Models.ShareDirectoryItem> items, string parentPath)> DisplayDirectoryTreeAsync(string currentPath = "")
        {
            Console.Clear();
            Console.WriteLine($"=== Azure File Share: {_shareClient.Name} - Directory Browser ===\n");
            Console.WriteLine($"Current location: /{currentPath}");
            Console.WriteLine("------------------------------------------------------------------");
            
            var items = await ListDirectoryContentsAsync(currentPath);
            
            // Sort items: directories first, then files, both alphabetically
            items.Sort((a, b) => {
                if (a.IsDirectory && !b.IsDirectory) return -1;
                if (!a.IsDirectory && b.IsDirectory) return 1;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            
            // Add navigation options
            string parentPath = "";
            if (!string.IsNullOrEmpty(currentPath))
            {
                // Calculate parent directory path
                var pathParts = currentPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                parentPath = string.Join("/", pathParts.Take(pathParts.Length - 1));
                
                // Add parent directory navigation option
                Console.WriteLine("0. [DIR] .. (Go to parent directory)");
            }
            
            // Display items with index
            int index = 1;
            foreach (var item in items)
            {
                Console.WriteLine($"{index}. {item}");
                index++;
            }
            
            Console.WriteLine("------------------------------------------------------------------");
            return (items, parentPath);
        }
        
        /// <summary>
        /// Allows the user to interactively browse and select a directory in the Azure File Share.
        /// </summary>
        /// <returns>The selected directory path</returns>
        public async Task<string> BrowseAndSelectDirectoryAsync()
        {
            string currentPath = "";
            bool selectionCompleted = false;
            
            while (!selectionCompleted)
            {
                var (items, parentPath) = await DisplayDirectoryTreeAsync(currentPath);
                
                Console.WriteLine("\nOptions:");
                Console.WriteLine("- Enter a number to navigate to that directory/file");
                Console.WriteLine("- Type 'select' to choose the current directory");
                Console.WriteLine("- Type 'exit' to cancel selection");
                Console.Write("\nYour choice: ");
                
                string input = Console.ReadLine()?.Trim() ?? "";
                
                if (input.Equals("select", StringComparison.OrdinalIgnoreCase))
                {
                    // User has selected the current directory
                    selectionCompleted = true;
                }
                else if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    // User wants to cancel
                    return "";
                }
                else if (int.TryParse(input, out int selectedIndex))
                {
                    if (selectedIndex == 0 && !string.IsNullOrEmpty(currentPath))
                    {
                        // Navigate to parent directory
                        currentPath = parentPath;
                    }
                    else if (selectedIndex > 0 && selectedIndex <= items.Count)
                    {
                        var selectedItem = items[selectedIndex - 1];
                        
                        if (selectedItem.IsDirectory)
                        {
                            // Navigate to selected directory
                            currentPath = selectedItem.Path;
                        }
                        else
                        {
                            // Cannot select a file, show message
                            Console.WriteLine("\n[WARNING] You selected a file. Please select a directory instead.");
                            Console.WriteLine("Press any key to continue...");
                            Console.ReadKey();
                        }
                    }
                    else
                    {
                        Console.WriteLine("\n[ERROR] Invalid selection. Press any key to try again.");
                        Console.ReadKey();
                    }
                }
                else
                {
                    Console.WriteLine("\n[ERROR] Invalid input. Press any key to try again.");
                    Console.ReadKey();
                }
            }
            
            return currentPath;
        }

        /// <summary>
        /// Lists files in the given file share directory, compares their MD5 hashes with local files,
        /// updates metadata if there is a mismatch, and exports both a full report and a mismatches-only report.
        /// </summary>
        /// <param name="localDirectory">Path to the local directory containing files to compare against.</param>
        /// <param name="csvFileName">Originally passed CSV file name (used for directory reference).</param>
        /// <param name="azureDirectoryPath">Optional path to a specific directory in the Azure File Share.</param>
        public async Task ListAndVerifyFilesAsync(string localDirectory, string csvFileName, string azureDirectoryPath = "")
        {
            var fileMetadataList = new List<FileMetadata>();
            var processedLocalFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // To track which local files we've processed
            var azureFilesDict = new Dictionary<string, (string hash, string path, long size)>(StringComparer.OrdinalIgnoreCase);

            try
            {
                // Get appropriate directory client based on the path
                ShareDirectoryClient directoryClient;
                if (string.IsNullOrEmpty(azureDirectoryPath))
                {
                    directoryClient = _shareClient.GetRootDirectoryClient();
                }
                else
                {
                    directoryClient = _shareClient.GetDirectoryClient(azureDirectoryPath);
                }

                try
                {
                    // First collect all Azure files into a dictionary for efficient lookup
                    await foreach (var fileItem in directoryClient.GetFilesAndDirectoriesAsync())
                    {
                        if (!fileItem.IsDirectory)
                        {
                            var fileClient = directoryClient.GetFileClient(fileItem.Name);

                            // Fetch file properties and retrieve the remote MD5 hash from metadata
                            var fileProperties = await fileClient.GetPropertiesAsync();
                            fileProperties.Value.Metadata.TryGetValue("md5", out string? remoteMD5);
                            string remotePath = fileClient.Uri.ToString();
                            long fileSize = fileProperties.Value.ContentLength;

                            // Store in dictionary for lookup
                            azureFilesDict[fileItem.Name] = (remoteMD5 ?? "", remotePath, fileSize);
                        }
                    }
                }
                catch (Azure.RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Forbidden)
                {
                    // Handle authorization errors specifically
                    string storageAccount = _shareServiceClient.Uri.Host.Split('.')[0];
                    throw new UnauthorizedAccessException(
                        $"Authorization Error: You don't have sufficient permissions to access the file share.\n\n" +
                        $"Please ensure your account or service principal has been granted the 'Storage File Data SMB Share Contributor' " +
                        $"role on the '{storageAccount}' storage account or the specific file share.", ex);
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
                                    var fileClient = directoryClient.GetFileClient(fileName);
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
                }
                  // Export results for all files
                string reportPath = Path.Combine(Directory.GetCurrentDirectory(), csvFileName);
                CsvExporter.ExportToCsv(fileMetadataList, reportPath);
                
                // Export mismatches-only results
                string mismatchReportName = Path.GetFileNameWithoutExtension(csvFileName) + "_Mismatches" + Path.GetExtension(csvFileName);
                string mismatchReportPath = Path.Combine(Directory.GetCurrentDirectory(), mismatchReportName);
                
                var mismatches = fileMetadataList.Where(m => m.Status != "Match").ToList();
                CsvExporter.ExportToCsv(mismatches, mismatchReportPath);
                
                // Output summary
                Console.WriteLine($"\n[INFO] File comparison complete. Found {fileMetadataList.Count} files total:");
                Console.WriteLine($"  - {fileMetadataList.Count(f => f.Status == "Match")} matching files");
                Console.WriteLine($"  - {fileMetadataList.Count(f => f.Status == "Mismatch")} mismatched files");
                Console.WriteLine($"  - {fileMetadataList.Count(f => f.Status == "AzureFileMissing")} files missing in Azure");
                Console.WriteLine($"  - {fileMetadataList.Count(f => f.Status == "LocalFileMissing")} files missing locally");
                
                Console.WriteLine($"\n[INFO] Full report saved to: {reportPath}");
                if (mismatches.Any())
                {
                    Console.WriteLine($"[INFO] Mismatches report saved to: {mismatchReportPath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] An error occurred during file verification: {ex.Message}");
                if (ex is UnauthorizedAccessException)
                {
                    Console.WriteLine(ex.Message); // The formatted authorization error message
                }
                else
                {
                    Console.WriteLine($"[DEBUG] {ex}");
                }
                throw;
            }
        }
    }
}

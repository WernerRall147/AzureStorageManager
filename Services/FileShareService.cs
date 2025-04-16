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
using Azure;

namespace AzureStorageManager.Services
{
    public class FileShareService
    {
        private readonly ShareServiceClient _shareServiceClient;
        private readonly ShareClient _shareClient;
        private readonly string _fileShareName;        /// <summary>
        /// Constructor without custom ShareClientOptions.
        /// </summary>
        public FileShareService(string serviceUri, string fileShareName, TokenCredential credential)
        {
            _fileShareName = fileShareName;
            
            // Create options with FileRequestIntentPolicy to ensure x-ms-file-request-intent header is sent
            var options = new ShareClientOptions();
            options.AddPolicy(new Utilities.FileRequestIntentPolicy(), HttpPipelinePosition.PerCall);
            
            _shareServiceClient = new ShareServiceClient(new Uri(serviceUri), credential, options);
            _shareClient = _shareServiceClient.GetShareClient(fileShareName);
        }        /// <summary>
        /// Constructor that accepts ShareClientOptions, allowing custom pipeline policies.
        /// </summary>        
        public FileShareService(string serviceUri, string fileShareName, TokenCredential credential, ShareClientOptions options)
        {
            _fileShareName = fileShareName;
            
            // Ensure the FileRequestIntentPolicy is added to handle the x-ms-file-request-intent header requirement
            options.AddPolicy(new Utilities.FileRequestIntentPolicy(), HttpPipelinePosition.PerCall);
            
            _shareServiceClient = new ShareServiceClient(new Uri(serviceUri), credential, options);
            _shareClient = _shareServiceClient.GetShareClient(fileShareName);
        }

        /// <summary>
        /// Verifies that the file share exists and creates it if needed
        /// </summary>
        /// <param name="createIfNotExists">Whether to create the file share if it doesn't exist</param>
        /// <returns>True if the share exists or was created, False if the share doesn't exist and wasn't created</returns>
        public async Task<bool> EnsureShareExistsAsync(bool createIfNotExists = false)
        {
            try
            {
                // Check if the file share exists using our enhanced method that handles header requirements properly
                var exists = await Utilities.FileShareClientExtensions.ExistsWithIntentHeaderAsync(_shareClient);
                
                if (!exists && createIfNotExists)
                {
                    Console.WriteLine($"[INFO] File share '{_fileShareName}' does not exist. Creating it now...");
                    await _shareClient.CreateAsync();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[SUCCESS] File share '{_fileShareName}' created successfully");
                    Console.ResetColor();
                    return true;
                }
                
                return exists;
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 403)
            {
                // Handle authorization errors with a more user-friendly message
                string storageAccount = _shareServiceClient.Uri.Host.Split('.')[0];
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Authorization Error: You don't have permission to access or create the file share.");                
                Console.WriteLine($"[ERROR] Please ensure your account has been granted the 'Storage File Data SMB Share Elevated Contributor' role on the '{storageAccount}' storage account.");
                Console.ResetColor();
                
                // Log detailed information for troubleshooting
                string detailedError = $"{ex.Message}\nTime:{DateTime.Now}\nStatus: {ex.Status} ({ex.ErrorCode})";
                Logger.LogError($"Authorization error checking/creating file share: {detailedError}");
                
                // Add more specific error analysis to help pinpoint the exact permission issue
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[DETAIL] Error code: {ex.ErrorCode}");
                
                if (ex.Message.Contains("This request is not authorized to perform this operation"))
                {
                    Console.WriteLine($"[DETAIL] The current authorization method doesn't have the required permissions.");
                    Console.WriteLine($"[DETAIL] Try running the File Share Permission Diagnostics (option 5 in main menu) to identify specific missing permissions.");
                }
                
                Console.ResetColor();
                return false;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Failed to verify or create file share: {ex.Message}");
                Console.ResetColor();
                Logger.LogError($"Error checking/creating file share: {ex.Message}");
                return false;
            }
        }        /// <summary>
        /// Gets a list of directories and files at the specified path in the Azure File Share.
        /// </summary>
        /// <param name="directoryPath">The path to the directory to list (empty for root).</param>
        /// <returns>A list of file and directory items.</returns>
        public async Task<List<Models.ShareDirectoryItem>> ListDirectoryContentsAsync(string directoryPath = "")
        {
            List<Models.ShareDirectoryItem> items = new List<Models.ShareDirectoryItem>();
            
            try
            {
                // First check if the share exists
                bool shareExists = await EnsureShareExistsAsync(false);
                if (!shareExists)
                {
                    throw new Azure.RequestFailedException(
                        404, 
                        $"The file share '{_fileShareName}' does not exist. Please verify the share name or use the create option.");
                }
                
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
            // First check if the share exists
            bool shareExists = await EnsureShareExistsAsync(false);
            if (!shareExists)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[WARNING] The file share '{_fileShareName}' does not exist.");
                Console.WriteLine($"Would you like to create this file share? (yes/no): ");
                Console.ResetColor();
                
                string response = Console.ReadLine()?.Trim().ToLower() ?? "";
                if (response == "yes" || response == "y")
                {
                    shareExists = await EnsureShareExistsAsync(true);
                    if (!shareExists)
                    {
                        Console.WriteLine("[ERROR] Failed to create file share. Using root directory instead.");
                        return "";
                    }
                }
                else
                {
                    Console.WriteLine("[INFO] File share not created. Using root directory instead.");
                    return "";
                }
            }
            
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

        /// <summary>
        /// Diagnoses Azure File Share permission issues by testing different operations
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task DiagnosePermissionsAsync()
        {
            // Dictionary to store test results
            var testResults = new Dictionary<string, bool>();
            
            // Set up a test directory and file path for diagnostics
            string testDirPath = "permission-diagnostics-" + DateTime.Now.ToString("yyyyMMddHHmmss");
            string testFilePath = testDirPath + "/test-file.txt";
            string testContent = "This is a test file for permission diagnostics.";
            
            Console.WriteLine("\n[TEST] Running permission diagnostics on File Share operations...");
              // Test 1: Check if share exists
            try
            {
                Console.Write("[TEST] 1. Checking if file share exists... ");
                // Use our custom extension method that ensures the header is present
                var exists = await Utilities.FileShareClientExtensions.ExistsWithIntentHeaderAsync(_shareClient);                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("SUCCESS");
                Console.ResetColor();
                testResults["Check share exists"] = true;
                Console.WriteLine($"       File share '{_fileShareName}' {(exists ? "exists" : "does not exist")}");
                
                // If the share doesn't exist, ask if we should create it for the diagnostics
                if (!exists)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[NOTICE] The file share '{_fileShareName}' doesn't exist, which will cause the remaining tests to fail.");
                    Console.Write($"[PROMPT] Would you like to create the file share for testing purposes? (yes/no): ");
                    Console.ResetColor();
                    
                    string response = Console.ReadLine()?.Trim().ToLower() ?? "no";
                    if (response == "yes" || response == "y")
                    {
                        try
                        {
                            Console.Write($"[INFO] Creating file share '{_fileShareName}'... ");
                            await _shareClient.CreateAsync();
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("SUCCESS");
                            Console.ResetColor();
                            Console.WriteLine($"[INFO] File share '{_fileShareName}' created successfully.");
                        }
                        catch (Exception ex)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("FAILED");
                            Console.ResetColor();
                            Console.WriteLine($"[ERROR] Failed to create file share: {ex.Message}");
                            Logger.LogError($"File share creation failed during diagnostics: {ex}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[INFO] Continuing with diagnostics. Some tests will be skipped due to missing file share.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("FAILED");
                Console.ResetColor();
                testResults["Check share exists"] = false;
                Console.WriteLine($"       Error: {ex.Message}");
                Logger.LogError($"File share existence check failed: {ex}");
            }
              // Test 2: Create directory
            ShareDirectoryClient? dirClient = null;
            try
            {
                Console.Write("[TEST] 2. Creating a test directory... ");
                dirClient = _shareClient.GetDirectoryClient(testDirPath);
                
                // Use the helper method to create the directory with the proper header
                await Utilities.FileShareClientExtensions.CreateDirectoryWithIntentHeaderAsync(dirClient);
                
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("SUCCESS");
                Console.ResetColor();
                testResults["Create directory"] = true;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("FAILED");
                Console.ResetColor();
                testResults["Create directory"] = false;
                Console.WriteLine($"       Error: {ex.Message}");
                Logger.LogError($"Directory creation failed: {ex}");
            }
            
            // Test 3: Upload a file
            ShareFileClient? fileClient = null;
            if (dirClient != null && testResults["Create directory"])
            {
                try
                {                    Console.Write("[TEST] 3. Uploading a test file... ");
                    fileClient = dirClient.GetFileClient("test-file.txt");
                    using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(testContent)))
                    {
                        // Use the helper method to create the file with the proper header
                        await Utilities.FileShareClientExtensions.CreateFileWithIntentHeaderAsync(fileClient, stream.Length);
                        await fileClient.UploadRangeAsync(new HttpRange(0, stream.Length), stream);
                    }
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("SUCCESS");
                    Console.ResetColor();
                    testResults["Upload file"] = true;
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("FAILED");
                    Console.ResetColor();
                    testResults["Upload file"] = false;
                    Console.WriteLine($"       Error: {ex.Message}");
                    Logger.LogError($"File upload failed: {ex}");
                }
            }
            else
            {
                Console.WriteLine("[TEST] 3. Uploading a test file... SKIPPED (directory creation failed)");
                testResults["Upload file"] = false;
            }
            
            // Test 4: Download file
            if (fileClient != null && testResults["Upload file"])
            {
                try
                {
                    Console.Write("[TEST] 4. Downloading the test file... ");
                    var downloadInfo = await fileClient.DownloadAsync();
                    using (var stream = new MemoryStream())
                    {
                        await downloadInfo.Value.Content.CopyToAsync(stream);
                        string downloadedContent = System.Text.Encoding.UTF8.GetString(stream.ToArray());
                        bool contentMatches = downloadedContent == testContent;
                        
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("SUCCESS");
                        Console.ResetColor();
                        testResults["Download file"] = true;
                        Console.WriteLine($"       Content verification: {(contentMatches ? "Passed" : "Failed")}");
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("FAILED");
                    Console.ResetColor();
                    testResults["Download file"] = false;
                    Console.WriteLine($"       Error: {ex.Message}");
                    Logger.LogError($"File download failed: {ex}");
                }
            }
            else
            {
                Console.WriteLine("[TEST] 4. Downloading the test file... SKIPPED (file upload failed)");
                testResults["Download file"] = false;
            }
            
            // Test 5: Delete file
            if (fileClient != null && testResults["Upload file"])
            {
                try
                {
                    Console.Write("[TEST] 5. Deleting the test file... ");
                    await fileClient.DeleteAsync();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("SUCCESS");
                    Console.ResetColor();
                    testResults["Delete file"] = true;
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("FAILED");
                    Console.ResetColor();
                    testResults["Delete file"] = false;
                    Console.WriteLine($"       Error: {ex.Message}");
                    Logger.LogError($"File deletion failed: {ex}");
                }
            }
            else
            {
                Console.WriteLine("[TEST] 5. Deleting the test file... SKIPPED (file upload failed)");
                testResults["Delete file"] = false;
            }
            
            // Test 6: Delete directory
            if (dirClient != null && testResults["Create directory"])
            {
                try
                {
                    Console.Write("[TEST] 6. Deleting the test directory... ");
                    await dirClient.DeleteAsync();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("SUCCESS");
                    Console.ResetColor();
                    testResults["Delete directory"] = true;
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("FAILED");
                    Console.ResetColor();
                    testResults["Delete directory"] = false;
                    Console.WriteLine($"       Error: {ex.Message}");
                    Logger.LogError($"Directory deletion failed: {ex}");
                }
            }
            else
            {
                Console.WriteLine("[TEST] 6. Deleting the test directory... SKIPPED (directory creation failed)");
                testResults["Delete directory"] = false;
            }
            
            // Display summary of test results
            Console.WriteLine("\n[SUMMARY] Azure File Share Permission Diagnostics Results:");
            Console.WriteLine("-----------------------------------------------------------");
            
            int passedTests = testResults.Count(r => r.Value);
            int totalTests = testResults.Count;
            
            foreach (var result in testResults)
            {
                if (result.Value)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✓ {result.Key}: Passed");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"✗ {result.Key}: Failed");
                }
                Console.ResetColor();
            }
            
            Console.WriteLine("-----------------------------------------------------------");
            Console.WriteLine($"Passed: {passedTests}/{totalTests} tests");
            
            // Provide specific role recommendations based on the test results
            Console.WriteLine();
            if (passedTests == totalTests)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[RESULT] All permission tests passed. Your current role has full access to this file share.");
                Console.ResetColor();
            }
            else if (!testResults["Check share exists"])
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[RESULT] You don't have permission to access this file share at all.");
                Console.WriteLine("[RECOMMENDATION] Request the 'Storage File Data Reader' role at minimum for read access,");
                Console.WriteLine("            or 'Storage File Data SMB Share Elevated Contributor' for full access.");
                Console.ResetColor();
            }
            else if (!testResults["Create directory"] || !testResults["Upload file"])
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[RESULT] You have read access but not sufficient permissions to create or modify content.");
                Console.WriteLine("[RECOMMENDATION] Request the 'Storage File Data SMB Share Elevated Contributor' role");
                Console.WriteLine("            instead of 'Storage File Data Privileged Contributor' that you might currently have.");
                Console.ResetColor();
            }
            else if (!testResults["Delete file"] || !testResults["Delete directory"])
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[RESULT] You have most permissions but can't delete content.");
                Console.WriteLine("[RECOMMENDATION] Request the 'Storage File Data SMB Share Elevated Contributor' role");
                Console.WriteLine("            for complete access to all file share operations.");
                Console.ResetColor();
            }
        }
    }
}

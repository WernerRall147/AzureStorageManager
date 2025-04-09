using System.Net;
using System.Net.Http;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models; 
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using AzureStorageManager.Services;
using AzureStorageManager.Models;
using AzureStorageManager.Utilities;
using System.Windows.Forms;
using System.Collections.Generic;

namespace AzureStorageManager
{    class Program
    {
        private static IConfiguration? _config;
        private static TokenCredential? _credential;
        private static HttpClient? _httpClient;

        static async Task Main(string[] args)
        {
            // Load configuration
            _config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddEnvironmentVariables()
                .Build();

            bool exitRequested = false;

            while (!exitRequested)
            {
                Console.Clear();
                DisplayMainMenu();
                
                string choice = Console.ReadLine()?.Trim() ?? "";

                switch (choice)
                {
                    case "1":
                        await VerifyFileIntegrityAsync();
                        break;
                    case "2":
                        await GenerateMD5HashesAsync();
                        break;
                    case "3":
                        await CheckAndUpdateMetadataAsync();
                        break;
                    case "4":
                        await CopyFilesToAzureAsync();
                        break;
                    case "5":
                        await DownloadFilesFromAzureAsync();
                        break;
                    case "6":
                        await GenerateConsolidatedReportAsync();
                        break;
                    case "7":
                        ViewLogs();
                        break;
                    case "8":
                        ConfigureSettings();
                        break;
                    case "9":
                        DisplayHelp();
                        break;
                    case "10":
                        exitRequested = true;
                        Console.WriteLine("Exiting the application. Goodbye!");
                        break;
                    default:
                        Console.WriteLine("Invalid choice. Please try again.");
                        break;
                }

                if (!exitRequested)
                {
                    Console.WriteLine("\nPress Enter to return to the main menu...");
                    Console.ReadLine();
                }
            }
        }

        private static void DisplayMainMenu()
        {
            Console.WriteLine("=======================================");
            Console.WriteLine("        Azure Storage Manager Tool      ");
            Console.WriteLine("=======================================");
            Console.WriteLine();
            Console.WriteLine("Please select an option:");
            Console.WriteLine("1. Verify File Integrity");
            Console.WriteLine("2. Generate MD5 Hashes for Local Files");
            Console.WriteLine("3. Check and Update Azure Metadata");
            Console.WriteLine("4. Copy Files to Azure");
            Console.WriteLine("5. Download Files from Azure");
            Console.WriteLine("6. Generate Consolidated Report");
            Console.WriteLine("7. View Logs");
            Console.WriteLine("8. Settings");
            Console.WriteLine("9. Help");
            Console.WriteLine("10. Exit");
            Console.WriteLine("=======================================");
            Console.Write("Enter your choice: ");
        }

        private static void DisplayIntroduction()
        {
            Console.WriteLine("=======================================");
            Console.WriteLine("        Azure Storage Manager Tool      ");
            Console.WriteLine("=======================================");
            Console.WriteLine();
            Console.WriteLine("This tool helps you verify file integrity by comparing local files with those stored in Azure.");
            Console.WriteLine("It checks that files in Azure Blob Storage or File Shares match your local copies.");
            Console.WriteLine();
            Console.WriteLine("How to Use:");
            Console.WriteLine("1. Enter your Azure Storage Account name.");
            Console.WriteLine("2. Choose the storage type (Blob or File Share) and specify the container/share name.");
            Console.WriteLine("3. Enter the local directory path where your files are stored.");
            Console.WriteLine("The tool will compare each file's MD5 hash with the stored hash in Azure and");
            Console.WriteLine("generate a report showing any mismatches.");
            Console.WriteLine();
        }

        private static string GetDirectoryFromDialog()
        {
            using (var folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Select the local directory for file operations";
                folderDialog.UseDescriptionForTitle = true;

                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    return folderDialog.SelectedPath;
                }
                else
                {
                    Console.WriteLine("No directory selected. Operation cancelled.");
                    return string.Empty;
                }
            }
        }

        private static async Task InitializeAzureCredentialsAsync()
        {
            if (_credential != null)
            {
                // Credentials already initialized
                return;
            }

            string tenantId = _config?["Azure:TenantId"] ?? "";
            string clientId = _config?["Azure:ClientId"] ?? "";
            string thumbprint = _config?["Azure:CertificateThumbprint"] ?? "";
            string clientSecret = _config?["Azure:ClientSecret"] ?? "";

            // Load certificate from Windows Certificate Store
            Console.WriteLine("Loading certificate from Windows Certificate Store...");
            X509Certificate2? certificate = null;
            using (var store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
            {
                store.Open(OpenFlags.ReadOnly);
                var certCollection = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, validOnly: false);

                if (certCollection.Count > 0)
                {
                    certificate = certCollection[0];
                    Console.WriteLine($"Certificate loaded successfully: {certificate.Subject}");
                }
                else
                {
                    Console.WriteLine("Certificate not found. Will attempt fallback to client secret authentication.");
                }

                store.Close();
            }

            // Initialize credentials: try certificate first, then fallback to client secret
            if (certificate != null)
            {
                Console.WriteLine("Initializing ClientCertificateCredential...");
                _credential = new ClientCertificateCredential(tenantId, clientId, certificate);
            }
            else
            {
                if (string.IsNullOrEmpty(clientSecret))
                {
                    // If client secret isn't in appsettings, check environment variable
                    clientSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET") ?? "";
                }

                if (string.IsNullOrEmpty(clientSecret))
                {
                    Console.WriteLine("No client secret provided and certificate not found. Cannot authenticate.");
                    throw new ApplicationException("Authentication failed - no valid credentials provided.");
                }

                Console.WriteLine("Initializing ClientSecretCredential...");
                _credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
            }

            // Ask if using a proxy
            Console.Write("Are you using a proxy? (yes/no): ");
            string? useProxyResponse = Console.ReadLine()?.Trim().ToLower();

            if (useProxyResponse == "yes")
            {
                Console.Write("Enter the proxy URL: ");
                string? proxyUrl = Console.ReadLine()?.Trim();

                Console.Write("Enter the proxy port: ");
                string? proxyPort = Console.ReadLine()?.Trim();

                if (!string.IsNullOrEmpty(proxyUrl) && !string.IsNullOrEmpty(proxyPort) && int.TryParse(proxyPort, out int port))
                {
                    var proxy = new WebProxy($"{proxyUrl}:{port}");
                    var httpClientHandler = new HttpClientHandler
                    {
                        Proxy = proxy,
                        UseProxy = true
                    };
                    _httpClient = new HttpClient(httpClientHandler);
                }
                else
                {
                    Console.WriteLine("Invalid proxy URL or port. Proceeding without proxy.");
                }
            }
        }

        private static async Task VerifyFileIntegrityAsync()
        {
            DisplayIntroduction();
            Console.WriteLine("=== Verify File Integrity ===\n");
            Console.WriteLine("[INFO] Starting file integrity verification process...");

            try
            {
                Console.WriteLine("[INFO] Initializing Azure credentials...");
                await InitializeAzureCredentialsAsync();
                Console.WriteLine("[SUCCESS] Azure credentials initialized successfully");
                
                // Prompt user for storage account details
                Console.Write("Enter your Azure Storage Account Name: ");
                string storageAccountName = Console.ReadLine() ?? "";
                Console.WriteLine($"[INFO] Using storage account: {storageAccountName}");

                Console.WriteLine("Choose Storage Type:");
                Console.WriteLine("1. Blob Storage");
                Console.WriteLine("2. File Share");
                string choice = Console.ReadLine() ?? "";
                Console.WriteLine($"[INFO] Selected storage type option: {choice}");

                Console.WriteLine("[INFO] Opening folder dialog to select local directory...");
                string localDirectory = GetDirectoryFromDialog();
                if (string.IsNullOrEmpty(localDirectory))
                {
                    Console.WriteLine("[WARNING] Directory selection canceled by user");
                    return;
                }
                Console.WriteLine($"[INFO] Selected local directory: {localDirectory}");
                Console.WriteLine($"[INFO] Directory contains {Directory.GetFiles(localDirectory, "*", SearchOption.AllDirectories).Length} files to process");

                if (string.IsNullOrEmpty(storageAccountName))
                {
                    Console.WriteLine("[ERROR] Storage account name is missing. Please check your input.");
                    return;
                }

                if (choice == "1")
                {
                    // Blob Storage
                    Console.Write("Enter your Azure Blob Container Name: ");
                    string blobContainerName = Console.ReadLine() ?? "";
                    Console.WriteLine($"[INFO] Using blob container: {blobContainerName}");

                    Console.WriteLine("[INFO] Connecting to Blob Storage service...");
                    var blobClientOptions = new BlobClientOptions();
                    if (_httpClient != null)
                    {
                        Console.WriteLine("[INFO] Using custom HTTP client with proxy configuration");
                        blobClientOptions.Transport = new HttpClientTransport(_httpClient);
                    }
                    
                    try {
                        Console.WriteLine($"[INFO] Creating connection to {storageAccountName}.blob.core.windows.net");
                        var blobServiceClient = new BlobServiceClient(new Uri($"https://{storageAccountName}.blob.core.windows.net"), _credential, blobClientOptions);
                        Console.WriteLine("[SUCCESS] Successfully created Blob service client");

                        Console.WriteLine($"[INFO] Instantiating BlobStorageService for container '{blobContainerName}'");
                        var blobStorageService = new BlobStorageService(blobServiceClient, blobContainerName);
                        string reportFileName = $"BlobStorageReport_{Path.GetFileName(localDirectory)}.csv";
                        Console.WriteLine($"[INFO] Report will be saved as: {reportFileName}");

                        Console.WriteLine("[INFO] Starting verification of local files against Azure blobs...");
                        Console.WriteLine("[INFO] This process may take some time depending on the number of files...");
                        await blobStorageService.ListAndVerifyBlobsAsync(localDirectory, reportFileName);
                        Console.WriteLine("[SUCCESS] Verification process completed successfully");
                        Console.WriteLine($"[INFO] Verification complete. Report saved to: {Path.Combine(Directory.GetCurrentDirectory(), reportFileName)}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Failed to connect to Blob storage: {ex.Message}");
                        Console.WriteLine($"[DEBUG] Exception details: {ex}");
                    }
                }
                else if (choice == "2")
                {
                    // File Share
                    Console.Write("Enter your Azure File Share Name: ");
                    string fileShareName = Console.ReadLine() ?? "";
                    Console.WriteLine($"[INFO] Using Azure File Share: {fileShareName}");

                    Console.Write("Enter the folder path within the Azure File Share (e.g., folder\\subfolder): ");
                    string azureFolderPath = Console.ReadLine() ?? "";
                    Console.WriteLine($"[INFO] Using folder path within share: {(string.IsNullOrEmpty(azureFolderPath) ? "root directory" : azureFolderPath)}");

                    Console.WriteLine("[INFO] Connecting to File Share service...");
                    var shareClientOptions = new ShareClientOptions();
                    if (_httpClient != null)
                    {
                        Console.WriteLine("[INFO] Using custom HTTP client with proxy configuration");
                        shareClientOptions.Transport = new HttpClientTransport(_httpClient);
                    }
                    
                    try {
                        shareClientOptions.AddPolicy(new FileRequestIntentPolicy(), HttpPipelinePosition.PerCall);                        Console.WriteLine($"[INFO] Creating connection to {storageAccountName}.file.core.windows.net");
                        if (_credential == null)
                        {
                            Console.WriteLine("[ERROR] Azure credentials are not initialized. Please run the credentials initialization first.");
                            return;
                        }
                        var shareServiceClient = new ShareServiceClient(new Uri($"https://{storageAccountName}.file.core.windows.net"), _credential, shareClientOptions);
                        Console.WriteLine("[SUCCESS] Successfully created File Share service client");

                        Console.WriteLine($"[INFO] Instantiating FileShareService for share '{fileShareName}'");
                        var fileShareService = new FileShareService($"https://{storageAccountName}.file.core.windows.net", fileShareName, _credential, shareClientOptions);
                        string reportFileName = $"FileShareReport_{Path.GetFileName(localDirectory)}.csv";
                        Console.WriteLine($"[INFO] Report will be saved as: {reportFileName}");

                        Console.WriteLine("[INFO] Starting verification of local files against Azure File Share...");
                        Console.WriteLine("[INFO] This process may take some time depending on the number of files...");
                        await fileShareService.ListAndVerifyFilesAsync(localDirectory, reportFileName);
                        Console.WriteLine("[SUCCESS] Verification process completed successfully");
                        Console.WriteLine($"[INFO] Verification complete. Report saved to: {Path.Combine(Directory.GetCurrentDirectory(), reportFileName)}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Failed to connect to File Share: {ex.Message}");
                        Console.WriteLine($"[DEBUG] Exception details: {ex}");
                    }
                }
                else
                {
                    Console.WriteLine("[ERROR] Invalid choice. Please select either option 1 or 2.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] An unexpected error occurred: {ex.Message}");
                Console.WriteLine($"[DEBUG] Exception details: {ex}");
                Logger.LogError($"VerifyFileIntegrityAsync failed: {ex}");
            }
        }

        private static async Task GenerateMD5HashesAsync()
        {
            Console.WriteLine("=== Generate MD5 Hashes for Local Files ===\n");
            Console.WriteLine("[INFO] Starting MD5 hash generation for local files...");
            
            Console.WriteLine("[INFO] Opening folder dialog to select local directory...");
            string localDirectory = GetDirectoryFromDialog();
            if (string.IsNullOrEmpty(localDirectory))
            {
                Console.WriteLine("[WARNING] Directory selection canceled by user");
                return;
            }
            Console.WriteLine($"[INFO] Selected directory: {localDirectory}");

            try
            {
                Console.WriteLine("[INFO] Scanning directory for files...");
                int totalFiles = Directory.GetFiles(localDirectory, "*", SearchOption.AllDirectories).Length;
                Console.WriteLine($"[INFO] Found {totalFiles} files to process");
                Console.WriteLine("[INFO] Generating MD5 hashes for all files in the selected directory...");
                
                // Create a list to hold file information
                var fileHashesList = new List<FileMetadata>();
                int processedCount = 0;
                int errorCount = 0;
                var startTime = DateTime.Now;
                
                Console.WriteLine("[INFO] Starting hash calculation process...");
                // Process all files recursively
                foreach (var filePath in Directory.GetFiles(localDirectory, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        string relativePath = Path.GetRelativePath(localDirectory, filePath);
                        Console.Write($"\r[PROCESSING] Calculating hash for file {processedCount+1} of {totalFiles}: {relativePath}");
                        
                        string md5Hash = FileHashUtility.CalculateMD5(filePath);
                        
                        fileHashesList.Add(new FileMetadata(
                            relativePath,
                            md5Hash,
                            "",  // No Azure hash since this is local only
                            "LocalOnly"
                        ));
                        
                        processedCount++;
                        if (processedCount % 10 == 0 || processedCount == totalFiles)
                        {
                            var elapsed = DateTime.Now - startTime;
                            var filesPerSecond = processedCount / (elapsed.TotalSeconds > 0 ? elapsed.TotalSeconds : 1);
                            var estTimeRemaining = TimeSpan.FromSeconds((totalFiles - processedCount) / (filesPerSecond > 0 ? filesPerSecond : 1));
                            
                            Console.Write($"\r[INFO] Processed {processedCount} of {totalFiles} files... ({filesPerSecond:F1} files/sec, Est. remaining: {estTimeRemaining.ToString(@"hh\:mm\:ss")})");
                        }
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        Console.WriteLine($"\n[ERROR] Failed to process {filePath}: {ex.Message}");
                        Logger.LogError($"Hash calculation error for {filePath}: {ex}");
                    }
                }
                
                Console.WriteLine(); // Clear the progress line
                Console.WriteLine($"[INFO] MD5 hash calculation completed for {processedCount} files ({errorCount} errors)");
                
                // Generate timestamp for report filename
                string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                string reportFileName = Path.Combine(localDirectory, $"LocalHashesReport_{timestamp}.csv");
                Console.WriteLine($"[INFO] Generating CSV report as {reportFileName}");
                
                // Export to CSV
                CsvExporter.ExportToCsv(fileHashesList, reportFileName);
                
                Console.WriteLine($"[SUCCESS] Hash generation complete. {processedCount} files processed, {errorCount} errors.");
                Console.WriteLine($"[INFO] Report saved to: {reportFileName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] An unexpected error occurred: {ex.Message}");
                Console.WriteLine($"[DEBUG] Exception details: {ex}");
                Logger.LogError($"GenerateMD5HashesAsync failed: {ex}");
            }
        }

        private static async Task CheckAndUpdateMetadataAsync()
        {
            Console.WriteLine("=== Check and Update Azure Metadata ===\n");
            Console.WriteLine("[INFO] Starting Azure metadata check and update process...");
            
            try
            {
                Console.WriteLine("[INFO] Initializing Azure credentials...");
                await InitializeAzureCredentialsAsync();
                Console.WriteLine("[SUCCESS] Azure credentials initialized successfully");
                
                // Prompt user for storage account details
                Console.Write("Enter your Azure Storage Account Name: ");
                string storageAccountName = Console.ReadLine() ?? "";
                Console.WriteLine($"[INFO] Using storage account: {storageAccountName}");

                if (string.IsNullOrEmpty(storageAccountName))
                {
                    Console.WriteLine("[ERROR] Storage account name is missing. Please check your input.");
                    return;
                }

                Console.WriteLine("Choose Storage Type:");
                Console.WriteLine("1. Blob Storage");
                Console.WriteLine("2. File Share");
                string choice = Console.ReadLine() ?? "";
                Console.WriteLine($"[INFO] Selected storage type option: {choice}");

                if (choice == "1")
                {
                    // Blob Storage
                    Console.Write("Enter your Azure Blob Container Name: ");
                    string blobContainerName = Console.ReadLine() ?? "";
                    Console.WriteLine($"[INFO] Using blob container: {blobContainerName}");

                    Console.WriteLine("[INFO] Opening folder dialog to select local directory for reference files...");
                    string localDirectory = GetDirectoryFromDialog();
                    if (string.IsNullOrEmpty(localDirectory))
                    {
                        Console.WriteLine("[WARNING] Directory selection canceled by user");
                        return;
                    }
                    Console.WriteLine($"[INFO] Selected local directory: {localDirectory}");

                    Console.WriteLine("[INFO] Connecting to Blob Storage service...");
                    var blobClientOptions = new BlobClientOptions();
                    if (_httpClient != null)
                    {
                        Console.WriteLine("[INFO] Using custom HTTP client with proxy configuration");
                        blobClientOptions.Transport = new HttpClientTransport(_httpClient);
                    }
                    
                    try 
                    {
                        Console.WriteLine($"[INFO] Creating connection to {storageAccountName}.blob.core.windows.net");
                        var blobServiceClient = new BlobServiceClient(new Uri($"https://{storageAccountName}.blob.core.windows.net"), _credential, blobClientOptions);
                        Console.WriteLine("[SUCCESS] Successfully created Blob service client");
                        
                        Console.WriteLine($"[INFO] Getting container client for '{blobContainerName}'...");
                        var containerClient = blobServiceClient.GetBlobContainerClient(blobContainerName);

                        // Check if container exists
                        Console.WriteLine("[INFO] Checking if container exists...");
                        bool containerExists = await containerClient.ExistsAsync();
                        if (!containerExists)
                        {
                            Console.WriteLine($"[ERROR] Container '{blobContainerName}' does not exist. Please check the container name.");
                            return;
                        }
                        Console.WriteLine($"[SUCCESS] Container '{blobContainerName}' found");

                        Console.WriteLine("[INFO] Starting check for blob metadata and updating missing MD5 hashes...");
                        Console.WriteLine("[INFO] This process may take some time for containers with many blobs...");
                        
                        int totalBlobs = 0;
                        int updatedCount = 0;
                        int alreadyHadMD5 = 0;
                        int errorCount = 0;
                        var startTime = DateTime.Now;

                        Console.WriteLine("[INFO] Enumerating blobs and checking metadata...");
                        await foreach (var blobItem in containerClient.GetBlobsAsync())
                        {
                            totalBlobs++;
                            if (totalBlobs % 10 == 0)
                            {
                                var elapsed = DateTime.Now - startTime;
                                var blobsPerSecond = totalBlobs / (elapsed.TotalSeconds > 0 ? elapsed.TotalSeconds : 1);
                                Console.Write($"\r[INFO] Processing blobs: {totalBlobs} checked so far... ({blobsPerSecond:F1} blobs/sec)");
                            }
                            
                            try
                            {
                                var blobClient = containerClient.GetBlobClient(blobItem.Name);
                                
                                // Get properties to check if MD5 metadata exists
                                Console.Write($"\r[INFO] Checking metadata for blob: {blobItem.Name}                                 ");
                                var properties = await blobClient.GetPropertiesAsync();
                                bool hasMD5 = properties.Value.Metadata.TryGetValue("md5", out string? md5Value);
                                
                                if (!hasMD5 || string.IsNullOrEmpty(md5Value))
                                {
                                    // Get the content MD5 from the blob properties if available
                                    string contentMD5 = "";
                                    if (properties.Value.ContentHash.Length > 0)
                                    {
                                        contentMD5 = BitConverter.ToString(properties.Value.ContentHash).Replace("-", "").ToLowerInvariant();
                                        Console.WriteLine($"\r[INFO] Found content hash for {blobItem.Name}: {contentMD5}");
                                    }
                                    
                                    // If content MD5 is available, use it
                                    if (!string.IsNullOrEmpty(contentMD5))
                                    {
                                        var metadata = new Dictionary<string, string>();
                                        foreach (var item in properties.Value.Metadata)
                                        {
                                            metadata[item.Key] = item.Value;
                                        }
                                        metadata["md5"] = contentMD5;
                                        
                                        Console.WriteLine($"\r[INFO] Updating MD5 metadata for: {blobItem.Name}");
                                        await blobClient.SetMetadataAsync(metadata);
                                        updatedCount++;
                                        Console.WriteLine($"\r[SUCCESS] Updated MD5 metadata for: {blobItem.Name}");
                                    }
                                    else
                                    {
                                        Console.WriteLine($"\r[WARNING] Cannot update MD5 for {blobItem.Name}: Content hash not available");
                                    }
                                }
                                else
                                {
                                    alreadyHadMD5++;
                                    Console.Write($"\r[INFO] Blob already has MD5 metadata: {blobItem.Name} ({md5Value})                      ");
                                }
                            }
                            catch (Exception ex)
                            {
                                errorCount++;
                                Console.WriteLine($"\r[ERROR] Failed to process blob {blobItem.Name}: {ex.Message}");
                                Logger.LogError($"Metadata update error for blob {blobItem.Name}: {ex}");
                            }
                        }
                        
                        Console.WriteLine("\n[INFO] Metadata check and update process complete.");
                        Console.WriteLine($"[SUMMARY] Total blobs checked: {totalBlobs}");
                        Console.WriteLine($"[SUMMARY] Blobs already having MD5 metadata: {alreadyHadMD5}");
                        Console.WriteLine($"[SUMMARY] Blobs updated with MD5 metadata: {updatedCount}");
                        Console.WriteLine($"[SUMMARY] Errors encountered: {errorCount}");
                        
                        if (errorCount > 0)
                        {
                            Console.WriteLine("[WARNING] Some blobs could not be updated. Check the logs for details.");
                        }
                        else if (updatedCount > 0)
                        {
                            Console.WriteLine("[SUCCESS] All metadata updates completed successfully.");
                        }
                        else if (alreadyHadMD5 == totalBlobs)
                        {
                            Console.WriteLine("[INFO] All blobs already had MD5 metadata. No updates needed.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Error connecting to blob container: {ex.Message}");
                        Logger.LogError($"Blob container connection error: {ex}");
                    }
                }
                else if (choice == "2")
                {
                    Console.WriteLine("File Share metadata update is not yet supported. Check back in a future version.");
                }
                else
                {
                    Console.WriteLine("Invalid choice. Please select a valid option.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Error: {ex.InnerException.Message}");
                }
            }
        }

        // Placeholders for other methods referenced in Main
        private static async Task CopyFilesToAzureAsync()
        {
            Console.WriteLine("=== Copy Files to Azure ===\n");
            // Implementation would go here
        }

        private static async Task DownloadFilesFromAzureAsync()
        {
            Console.WriteLine("=== Download Files From Azure ===\n");
            // Implementation would go here
        }

        private static async Task GenerateConsolidatedReportAsync()
        {
            Console.WriteLine("=== Generate Consolidated Report ===\n");
            // Implementation would go here
        }

        private static void ViewLogs()
        {
            Console.WriteLine("=== View Logs ===\n");
            // Implementation would go here
        }

        private static void ConfigureSettings()
        {
            Console.WriteLine("=== Configure Settings ===\n");
            // Implementation would go here
        }

        private static void DisplayHelp()
        {
            Console.WriteLine("=== Help ===\n");
            // Implementation would go here
        }
    }
}

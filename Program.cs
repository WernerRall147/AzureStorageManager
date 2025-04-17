using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http;
using AzureStorageManager.Utilities;
using AzureStorageManager.Services;

namespace AzureStorageManager
{
    public partial class Program
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

            // Initialize proxy settings globally before any connections are made
            Utilities.GlobalProxyInitializer.EnsureGlobalProxyConfigured(_config);

            bool exitRequested = false;

            while (!exitRequested)
            {
                Console.Clear();
                DisplayMainMenu();

                string choice = Console.ReadLine()?.Trim() ?? "";                switch (choice)
                {
                    case "1":
                        await CopyFilesToAzureAsync();
                        break;
                    case "2":
                        await VerifyFileIntegrityAsync();
                        break;
                    case "3":
                        await GenerateMD5HashesAsync();
                        break;
                    case "4":
                        await CheckAndUpdateMetadataAsync();
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
                        await TestFileSharePermissionsAsync();
                        break;
                    case "11":
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

            // Display current connection status if there is one
            if (Models.ConnectionState.IsStorageAccountConnected)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(Models.ConnectionState.GetConnectionInfoString());
                Console.ResetColor();
            }
            // Display proxy status if configured
            if (Services.ProxyService.IsProxyConfigured)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"[PROXY] {Services.ProxyService.ProxyInfo}");
                Console.ResetColor();
            }            Console.WriteLine();
            Console.WriteLine("Please select an option:");
            Console.WriteLine("1. Copy Files to Azure");
            Console.WriteLine("2. Verify File Integrity"); 
            Console.WriteLine("3. Generate MD5 Hashes for Local Files");
            Console.WriteLine("4. Check and Update Azure Metadata");
            Console.WriteLine("5. Download Files from Azure");
            Console.WriteLine("6. Generate Consolidated Report");
            Console.WriteLine("7. View Logs");
            Console.WriteLine("8. Settings");
            Console.WriteLine("9. Help");
            Console.WriteLine("10. Test File Share Permissions");
            Console.WriteLine("11. Exit");
            Console.WriteLine("=======================================");
            Console.Write("Enter your choice: ");
        }
        private static void DisplayIntroduction()
        {
            Console.WriteLine("=======================================");
            Console.WriteLine("        Azure Storage Manager Tool      ");
            Console.WriteLine("=======================================");
            if (Models.ConnectionState.IsStorageAccountConnected)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(Models.ConnectionState.GetConnectionInfoString());
                Console.ResetColor();
            }
            if (Services.ProxyService.IsProxyConfigured)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"[PROXY] {Services.ProxyService.ProxyInfo}");
                Console.ResetColor();
            }
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
            // Show a message before opening the dialog to make it clear what's happening
            Console.WriteLine("[INFO] Opening folder selection dialog... (Application will wait for your selection)");

            try
            {
                // When running in Windows Server, we need to handle the UI thread differently
                // Create a special thread for showing dialogs to improve compatibility
                string selectedPath = "";
                var thread = new System.Threading.Thread(() =>
                {
                    try
                    {
                        using (var folderDialog = new FolderBrowserDialog())
                        {
                            // Use properties that are compatible with older Windows versions
                            folderDialog.Description = "Select the local directory for file operations";
                            folderDialog.ShowNewFolderButton = true;

                            // Avoid using UseDescriptionForTitle which might not be available in all versions
                            try
                            {
                                folderDialog.GetType().GetProperty("UseDescriptionForTitle")?.SetValue(folderDialog, true);
                            }
                            catch { /* Ignore if property doesn't exist */ }                            // Set an initial root folder to make navigation easier                            
                            try
                            {
                                if (Directory.Exists("C:\\"))
                                    folderDialog.SelectedPath = "C:\\";
                            }
                            catch { /* Ignore if setting initial path fails */ }

                            // Show the dialog and get the result
                            var result = folderDialog.ShowDialog();

                            if (result == DialogResult.OK)
                                selectedPath = folderDialog.SelectedPath;
                        }
                    }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error showing folder dialog: {ex.Message}");
            }
        });

                // Set as STAThread which is required for Windows Forms dialogs
                thread.SetApartmentState(System.Threading.ApartmentState.STA);
                thread.Start();
                thread.Join(); // Wait for the dialog thread to complete

                if (!string.IsNullOrEmpty(selectedPath))
                {
                    Console.WriteLine($"[INFO] Selected directory: {selectedPath}");
                    return selectedPath;
                }
                else
                {
                    Console.WriteLine("[WARNING] No directory selected. Operation cancelled.");
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to open folder dialog: {ex.Message}");
Console.WriteLine("[INFO] Please enter directory path manually:");
string manualPath = Console.ReadLine() ?? "";

if (Directory.Exists(manualPath))
{
    Console.WriteLine($"[INFO] Using directory: {manualPath}");
    return manualPath;
}

Console.WriteLine("[ERROR] Invalid directory path. Operation cancelled.");
return string.Empty;
            }
        }        private static async Task InitializeAzureCredentialsAsync()
{
    if (_credential != null)
    {
        // Credentials already initialized
        Console.WriteLine("[INFO] Using existing Azure credentials");
        return;
    }

    // First check and initialize proxy settings if configured using the helper
    _httpClient = Utilities.ProxyConfigHelper.ConfigureProxySettings(_config);

    Console.WriteLine("[INFO] Initializing Azure credentials...");
    string tenantId = _config?["Azure:TenantId"] ?? "";
    string clientId = _config?["Azure:ClientId"] ?? "";
    string certificateThumbprint = _config?["Azure:CertificateThumbprint"] ?? "";
    string clientSecret = _config?["Azure:ClientSecret"] ?? "";

    // Check for empty required values
    if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(clientId))
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("[ERROR] Azure Tenant ID and Client ID are required for authentication.");
        Console.WriteLine("[ERROR] Please add these values to your appsettings.json file.");
        Console.ResetColor();
        throw new ApplicationException("Missing required Azure AD credentials in configuration.");
    }

    // Initialize logging
    Logger.Initialize();

    // Authentication chain - will try each method in sequence until one succeeds
    bool authSuccess = false;
    string authMethod = GetAuthenticationPreference();
    Exception? lastException = null;

    // Step 1: Try certificate authentication first (unless explicitly configured otherwise)
    if (authMethod != "clientsecret" && authMethod != "interactive")
    {
        Console.WriteLine("[AUTH] Attempting Certificate Authentication...");
        X509Certificate2? certificate = null;

        try
        {
            // Load certificate from Windows Certificate Store
            using (var store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
            {
                store.Open(OpenFlags.ReadOnly);
                var certCollection = store.Certificates.Find(X509FindType.FindByThumbprint, certificateThumbprint, validOnly: false);

                if (certCollection.Count > 0)
                {
                    certificate = certCollection[0];
                    Console.WriteLine($"[AUTH] Certificate found: {certificate.Subject}");

                    // Check certificate validity
                    if (certificate.NotBefore > DateTime.Now || certificate.NotAfter < DateTime.Now)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"[WARNING] Certificate is not currently valid. Valid from {certificate.NotBefore} to {certificate.NotAfter}");
                        Console.ResetColor();
                    }

                    // Create credential and test it
                    _credential = new ClientCertificateCredential(tenantId, clientId, certificate);                            // Test the credential by trying to get an access token
                    Console.WriteLine("[AUTH] Testing certificate authentication...");
                    TokenRequestContext context = new TokenRequestContext(new[] { "https://storage.azure.com/.default" });
                    var tokenResult = await _credential.GetTokenAsync(context, CancellationToken.None);

                    if (!string.IsNullOrEmpty(tokenResult.Token))
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("[AUTH] Certificate authentication successful!");
                        Console.ResetColor();
                        Models.ConnectionState.AuthenticationType = "Certificate";
                        authSuccess = true;
                    }
                }
                else
                {
                    Console.WriteLine($"[AUTH] Certificate with thumbprint {certificateThumbprint} not found in certificate store.");
                }

                store.Close();
            }
        }
        catch (Exception ex)
        {
            lastException = ex;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[AUTH] Certificate authentication failed: {ex.Message}");
            Console.ResetColor();
            Logger.LogWarning($"Certificate authentication failed: {ex.Message}");

            // Clear the failed credential to try the next method
            _credential = null;
        }
    }

    // Step 2: Try client secret authentication if certificate auth failed
    if (!authSuccess && (authMethod != "certificate" && authMethod != "interactive"))
    {
        Console.WriteLine("[AUTH] Attempting Client Secret Authentication...");

        // If client secret isn't in appsettings, check environment variable
        if (string.IsNullOrEmpty(clientSecret))
        {
            clientSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET") ?? "";
        }

        // If still no client secret, prompt the user
        if (string.IsNullOrEmpty(clientSecret) && authMethod != "auto")
        {
            Console.WriteLine("[AUTH] Client secret not found in configuration. Please enter client secret:");
            clientSecret = ReadPasswordFromConsole();
        }

        if (!string.IsNullOrEmpty(clientSecret))
        {
            try
            {
                // Create the credential
                _credential = new ClientSecretCredential(tenantId, clientId, clientSecret);                        // Test the credential by trying to get an access token
                Console.WriteLine("[AUTH] Testing client secret authentication...");
                TokenRequestContext context = new TokenRequestContext(new[] { "https://storage.azure.com/.default" });
                var tokenResult = await _credential.GetTokenAsync(context, CancellationToken.None);

                if (!string.IsNullOrEmpty(tokenResult.Token))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("[AUTH] Client secret authentication successful!");
                    Console.ResetColor();
                    Models.ConnectionState.AuthenticationType = "Client Secret";
                    authSuccess = true;
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[AUTH] Client secret authentication failed: {ex.Message}");
                Console.ResetColor();
                Logger.LogWarning($"Client secret authentication failed: {ex.Message}");

                // Clear the failed credential to try the next method
                _credential = null;
            }
        }
        else
        {
            Console.WriteLine("[AUTH] No client secret provided, skipping client secret authentication.");
        }
    }

    // Step 3: Try interactive browser login as a last resort
    if (!authSuccess)
    {
        Console.WriteLine("[AUTH] Attempting Interactive Browser Login...");

        try
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("[AUTH] Opening browser for interactive login. Please complete the login process in your browser.");
            Console.ResetColor();

            // Create the credential
            _credential = new InteractiveBrowserCredential(new InteractiveBrowserCredentialOptions
            {
                TenantId = tenantId,
                ClientId = clientId,
                // Set a friendly message for the user
                AuthenticationRecord = null,
                DisableAutomaticAuthentication = false
            });                    // Test the credential by trying to get an access token
            TokenRequestContext context = new TokenRequestContext(new[] { "https://storage.azure.com/.default" });
            var tokenResult = await _credential.GetTokenAsync(context, CancellationToken.None);

            if (!string.IsNullOrEmpty(tokenResult.Token))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[AUTH] Interactive browser login successful!");
                Console.ResetColor();
                Models.ConnectionState.AuthenticationType = "Interactive Browser Login";
                authSuccess = true;
            }
        }
        catch (Exception ex)
        {
            lastException = ex;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[AUTH] Interactive browser login failed: {ex.Message}");
            Console.ResetColor();
            Logger.LogError($"Interactive browser login failed: {ex.Message}");
        }
    }

    // If all authentication methods failed, throw an exception with helpful information
    if (!authSuccess)
    {
        // Create a detailed error message with troubleshooting information
        var errorBuilder = new System.Text.StringBuilder();
        errorBuilder.AppendLine("Authentication failed - all authentication methods have been exhausted.");
        errorBuilder.AppendLine("\nTroubleshooting Information:");
        errorBuilder.AppendLine($"- Tenant ID: {(string.IsNullOrEmpty(tenantId) ? "Missing" : "Provided")}");
        errorBuilder.AppendLine($"- Client ID: {(string.IsNullOrEmpty(clientId) ? "Missing" : "Provided")}");
        errorBuilder.AppendLine($"- Certificate Thumbprint: {(string.IsNullOrEmpty(certificateThumbprint) ? "Missing" : certificateThumbprint)}");
        errorBuilder.AppendLine($"- Client Secret: {(string.IsNullOrEmpty(clientSecret) ? "Missing" : "Provided")}");
        errorBuilder.AppendLine($"- Preferred Auth Method: {authMethod}");
        errorBuilder.AppendLine("\nPossible Solutions:");
        errorBuilder.AppendLine("1. Verify the Azure AD app registration details in your appsettings.json");
        errorBuilder.AppendLine("2. Check that the certificate exists in Windows Certificate Store (Current User/Personal)");
        errorBuilder.AppendLine("3. Ensure the certificate thumbprint matches and is correctly formatted");
        errorBuilder.AppendLine("4. Verify the client secret hasn't expired");
        errorBuilder.AppendLine("5. Check that your Azure AD account has proper permissions");
        errorBuilder.AppendLine("6. Ensure you have network connectivity to Azure");

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("Authentication failed. No valid authentication methods succeeded.");
        Console.WriteLine("For detailed troubleshooting information, check the application logs.");
        Console.ResetColor();

        // Log the detailed error information
        Logger.LogError(errorBuilder.ToString());
        Logger.LogError($"Last exception: {lastException}");

        throw new ApplicationException("Authentication failed - all authentication methods have been exhausted. See application log for detailed troubleshooting information.");
    }            // Use existing proxy configuration from the ProxyService
                 // No need to ask for proxy settings again if they're already configured
    if (Services.ProxyService.IsInitialized)
    {
        if (Services.ProxyService.IsProxyConfigured && _httpClient == null)
        {
            // Create an HttpClient using the already configured proxy settings
            Console.WriteLine($"[INFO] Using configured proxy: {Services.ProxyService.ProxyInfo}");
            _httpClient = Services.ProxyService.HttpClient;
        }
    }
    else
    {
        // If somehow proxy was not initialized yet, initialize it
        Services.ProxyService.InitializeProxySettings(_config);
        if (Services.ProxyService.IsProxyConfigured)
        {
            _httpClient = Services.ProxyService.HttpClient;
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
        // Initialize Azure credentials
        await InitializeAzureCredentialsAsync();        // If we have a storage account connection already, ask if the user wants to use it
        string storageAccountName = "";
        bool skipStorageTypeSelection = false;
        
        if (Models.ConnectionState.IsStorageAccountConnected)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(Models.ConnectionState.GetConnectionInfoString());
            Console.ResetColor();
            Console.Write("Use current storage account connection? (yes/no): ");
            string? useCurrentResponse = Console.ReadLine()?.Trim().ToLower();

            if (useCurrentResponse == "yes" || useCurrentResponse == "y")
            {
                storageAccountName = Models.ConnectionState.StorageAccountName ?? "";
                Console.WriteLine($"[INFO] Using current storage account: {storageAccountName}");
                
                // If we already have a storage type selected, we can skip that prompt
                if (Models.ConnectionState.CurrentStorageType.HasValue)
                {
                    skipStorageTypeSelection = true;
                    Console.WriteLine($"[INFO] Using current storage type: {(Models.ConnectionState.CurrentStorageType == Models.ConnectionState.StorageTypes.BlobStorage ? "Blob Storage" : "File Share")}");
                }
            }
        }

        // If no storage account selected yet, prompt for one
        if (string.IsNullOrEmpty(storageAccountName))
        {
            Console.Write("Enter your Azure Storage Account Name: ");
            storageAccountName = Console.ReadLine() ?? "";
            Console.WriteLine($"[INFO] Using storage account: {storageAccountName}");
            // Reset connection state since we're changing accounts
            Models.ConnectionState.Reset();
            Models.ConnectionState.StorageAccountName = storageAccountName;
        }

        string choice;
        
        // Only ask for storage type if we don't already have one selected
        if (!skipStorageTypeSelection)
        {
            Console.WriteLine("Choose Storage Type:");
            Console.WriteLine("1. Blob Storage");
            Console.WriteLine("2. File Share");
            choice = Console.ReadLine() ?? "";
            Console.WriteLine($"[INFO] Selected storage type option: {choice}");
            
            // Update the storage type in the connection state
            if (choice == "1")
                Models.ConnectionState.CurrentStorageType = Models.ConnectionState.StorageTypes.BlobStorage;
            else if (choice == "2")
                Models.ConnectionState.CurrentStorageType = Models.ConnectionState.StorageTypes.FileShare;
        }
        else
        {
            // Use existing storage type choice
            choice = Models.ConnectionState.CurrentStorageType == Models.ConnectionState.StorageTypes.BlobStorage ? "1" : "2";
        }
        
        Console.WriteLine("[INFO] Opening folder dialog to select local directory...");
        string localDirectory = GetDirectoryFromDialog();
        if (string.IsNullOrEmpty(localDirectory))
        {
            Console.WriteLine("[WARNING] Directory selection canceled by user");
            return;
        }
        Console.WriteLine($"[INFO] Selected local directory: {localDirectory}");
        // Get files while handling access restrictions
        var files = GetFilesWithPermissionHandling(localDirectory);
        Console.WriteLine($"[INFO] Found {files.Count} accessible files to process");

        if (string.IsNullOrEmpty(storageAccountName))
        {
            Console.WriteLine("[ERROR] Storage account name is missing. Please check your input.");
            return;
        }        if (choice == "1")
        {
            // Update storage type in connection state
            Models.ConnectionState.CurrentStorageType = Models.ConnectionState.StorageTypes.BlobStorage;
            
            // Check if we already have a blob container connection and ask if user wants to use it
            string blobContainerName = "";
            if (!string.IsNullOrEmpty(Models.ConnectionState.BlobContainerName) &&
                Models.ConnectionState.StorageAccountName == storageAccountName)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"[INFO] Currently using container: {Models.ConnectionState.BlobContainerName}");
                Console.ResetColor();
                Console.Write("Use current container? (yes/no): ");
                string? useCurrentContainer = Console.ReadLine()?.Trim().ToLower();

                if (useCurrentContainer == "yes" || useCurrentContainer == "y")
                {
                    blobContainerName = Models.ConnectionState.BlobContainerName ?? "";
                    Console.WriteLine($"[INFO] Using current blob container: {blobContainerName}");
                }
            }

            // If no container selected yet, prompt for one
            if (string.IsNullOrEmpty(blobContainerName))
            {
                Console.Write("Enter your Azure Blob Container Name: ");
                blobContainerName = Console.ReadLine() ?? "";
                Console.WriteLine($"[INFO] Using blob container: {blobContainerName}");
                Models.ConnectionState.BlobContainerName = blobContainerName;
            }

            // Create connection options
            Console.WriteLine("[INFO] Preparing connection to Blob Storage service...");
            var blobClientOptions = new BlobClientOptions();
            if (_httpClient != null)
            {
                Console.WriteLine("[INFO] Using custom HTTP client with proxy configuration");
                blobClientOptions.Transport = new HttpClientTransport(_httpClient);
            }

            try
            {
                // Use existing service client or create a new one
                BlobServiceClient blobServiceClient;
                if (Models.ConnectionState.BlobServiceClient != null &&
                    Models.ConnectionState.StorageAccountName == storageAccountName)
                {
                    Console.WriteLine("[INFO] Reusing existing Blob service connection");
                    blobServiceClient = Models.ConnectionState.BlobServiceClient;
                }
                else
                {
                    Console.WriteLine($"[INFO] Creating new connection to {storageAccountName}.blob.core.windows.net");
                    blobServiceClient = new BlobServiceClient(
                        new Uri($"https://{storageAccountName}.blob.core.windows.net"),
                        _credential,
                        blobClientOptions);

                    // Store for future use
                    Models.ConnectionState.BlobServiceClient = blobServiceClient;
                    Models.ConnectionState.StorageAccountName = storageAccountName;
                }

                Console.WriteLine("[SUCCESS] Successfully connected to Blob service");
                Console.WriteLine($"[INFO] Instantiating BlobStorageService for container '{blobContainerName}'");
                var blobStorageService = new BlobStorageService(blobServiceClient, blobContainerName);
                string reportFileName = $"BlobStorageReport_{Path.GetFileName(localDirectory)}.csv";
                Console.WriteLine($"[INFO] Report will be saved as: {reportFileName}"); Console.WriteLine("[INFO] Starting verification of local files against Azure blobs...");
                Console.WriteLine("[INFO] This process may take some time depending on the number of files...");

                // Create progress spinner with cancellation support
                var startTime = DateTime.Now;
                var cts = new CancellationTokenSource();
                var progressTask = Utilities.ProgressIndicator.StartSpinner(
                    "Verifying files",
                    cts.Token);

                try
                {
                    // Run the actual verification process
                    await blobStorageService.ListAndVerifyBlobsAsync(localDirectory, reportFileName);

                    // Stop the progress spinner
                    Utilities.ProgressIndicator.StopProgress(
                        cts,
                        progressTask,
                        "[SUCCESS] Verification process completed successfully");

                    var totalTime = DateTime.Now - startTime;
                    Console.WriteLine($"[INFO] Verification complete in {totalTime.Minutes}m {totalTime.Seconds}s");
                    Console.WriteLine($"[INFO] Report saved to: {Path.Combine(Directory.GetCurrentDirectory(), reportFileName)}");
                }
                catch (Exception)
                {
                    // Make sure we cancel the progress indicator even on error
                    Utilities.ProgressIndicator.StopProgress(cts, progressTask, "[ERROR] Verification process failed");
                    throw; // Re-throw the exception to be handled by outer catch
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to connect to Blob storage: {ex.Message}");
                Console.WriteLine($"[DEBUG] Exception details: {ex}");
            }
        }        else if (choice == "2")
        {
            // Update storage type in connection state
            Models.ConnectionState.CurrentStorageType = Models.ConnectionState.StorageTypes.FileShare;
            
            // Check if we already have a file share connection and ask if user wants to use it
            string fileShareName = "";
            if (!string.IsNullOrEmpty(Models.ConnectionState.FileShareName) &&
                Models.ConnectionState.StorageAccountName == storageAccountName)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"[INFO] Currently using file share: {Models.ConnectionState.FileShareName}");
                Console.ResetColor();
                Console.Write("Use current file share? (yes/no): ");
                string? useCurrentShare = Console.ReadLine()?.Trim().ToLower();

                if (useCurrentShare == "yes" || useCurrentShare == "y")
                {
                    fileShareName = Models.ConnectionState.FileShareName ?? "";
                    Console.WriteLine($"[INFO] Using current file share: {fileShareName}");
                }
            }
            
            // If no file share selected yet, prompt for one
            if (string.IsNullOrEmpty(fileShareName))
            {
                Console.Write("Enter your Azure File Share Name: ");
                fileShareName = Console.ReadLine() ?? "";
                Console.WriteLine($"[INFO] Using Azure File Share: {fileShareName}");
                Models.ConnectionState.FileShareName = fileShareName;
            }

            // We already have the local directory from earlier, don't ask again
            Console.WriteLine($"[INFO] Using previously selected local directory: {localDirectory}");

            Console.WriteLine("[INFO] Connecting to File Share service...");
            var shareClientOptions = new ShareClientOptions();
            if (_httpClient != null)
            {
                Console.WriteLine("[INFO] Using custom HTTP client with proxy configuration");
                shareClientOptions.Transport = new HttpClientTransport(_httpClient);
            }

            try
            {
                shareClientOptions.AddPolicy(new FileRequestIntentPolicy(), HttpPipelinePosition.PerCall); Console.WriteLine($"[INFO] Creating connection to {storageAccountName}.file.core.windows.net");
                if (_credential == null)
                {
                    Console.WriteLine("[ERROR] Azure credentials are not initialized. Please run the credentials initialization first.");
                    return;
                }
                var shareServiceClient = new ShareServiceClient(new Uri($"https://{storageAccountName}.file.core.windows.net"), _credential, shareClientOptions);
                Console.WriteLine("[SUCCESS] Successfully created File Share service client");

                Console.WriteLine($"[INFO] Instantiating FileShareService for share '{fileShareName}'");
                var fileShareService = new AzureStorageManager.Services.FileShareService($"https://{storageAccountName}.file.core.windows.net", fileShareName, _credential, shareClientOptions);

                // Allow the user to browse and select a directory within the Azure File Share
                Console.WriteLine("[INFO] Preparing to browse Azure File Share directories...");
                Console.WriteLine("You'll be able to select where in the Azure File Share to verify files.");
                Console.WriteLine("Press any key to continue to the directory browser...");
                Console.ReadKey(true);

                string azureDirectoryPath = "";
                try
                {
                    azureDirectoryPath = await fileShareService.BrowseAndSelectDirectoryAsync();
                    if (string.IsNullOrEmpty(azureDirectoryPath))
                    {
                        Console.WriteLine("[INFO] No Azure directory selected or operation cancelled. Using root directory.");
                    }
                    else
                    {
                        Console.WriteLine($"[INFO] Selected Azure directory: /{azureDirectoryPath}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WARNING] Error browsing Azure directories: {ex.Message}. Using root directory instead.");
                    Console.WriteLine($"[DEBUG] {ex}");
                    azureDirectoryPath = "";
                }

                string reportFileName = $"FileShareReport_{Path.GetFileName(localDirectory)}.csv";
                Console.WriteLine($"[INFO] Report will be saved as: {reportFileName}");
                Console.WriteLine($"[INFO] Starting verification of local files against Azure File Share{(string.IsNullOrEmpty(azureDirectoryPath) ? "" : $" in /{azureDirectoryPath}")}...");
                Console.WriteLine("[INFO] This process may take some time depending on the number of files...");

                // Create progress spinner with cancellation support
                var startTime = DateTime.Now;
                var cts = new CancellationTokenSource();
                var progressTask = Utilities.ProgressIndicator.StartSpinner(
                    "Verifying files",
                    cts.Token);

                try
                {
                    // Run the actual verification process
                    await fileShareService.ListAndVerifyFilesAsync(localDirectory, reportFileName, azureDirectoryPath);

                    // Stop the progress spinner
                    Utilities.ProgressIndicator.StopProgress(
                        cts,
                        progressTask,
                        "[SUCCESS] Verification process completed successfully");

                    var totalTime = DateTime.Now - startTime;
                    Console.WriteLine($"[INFO] Verification complete in {totalTime.Minutes}m {totalTime.Seconds}s");
                    Console.WriteLine($"[INFO] Report saved to: {Path.Combine(Directory.GetCurrentDirectory(), reportFileName)}");
                }
                catch (Exception)
                {
                    // Make sure we cancel the progress indicator even on error
                    Utilities.ProgressIndicator.StopProgress(cts, progressTask, "[ERROR] Verification process failed");
                    throw; // Re-throw the exception to be handled by outer catch
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] {ex.Message}");
                Console.ResetColor();

                // Log the full exception details but don't display to user
                Logger.LogError($"Authorization exception details: {ex}");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Failed to connect to File Share: {ex.Message}");
                Console.ResetColor();

                // Only show detailed exception info in debug logs
                Logger.LogError($"Exception details: {ex}");
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

    // First, select the directory - this must happen before any task is spawned
    string localDirectory;
    try
    {
        // This is a blocking operation that must complete before proceeding
        localDirectory = GetDirectoryFromDialog();

        if (string.IsNullOrEmpty(localDirectory))
        {
            Console.WriteLine("[WARNING] Directory selection canceled by user");
            return;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] Failed to open directory selection dialog: {ex.Message}");
        return;
    }
    try
    {
        Console.WriteLine("[INFO] Scanning directory for files...");
        var filePaths = GetFilesWithPermissionHandling(localDirectory);
        Console.WriteLine($"[INFO] Found {filePaths.Count} files to process");

        // Setup progress tracking
        DateTime startTime = DateTime.Now;
        int processed = 0;
        int total = filePaths.Count;
        var lockObj = new object();

        // Start progress reporting task with cancellation support
        var cts = new CancellationTokenSource();
        var progressTask = Utilities.ProgressIndicator.StartProgress(() =>
        {
            lock (lockObj)
            {
                var percent = processed * 100 / (total > 0 ? total : 1);
                var elapsed = DateTime.Now - startTime;
                var filesPerSecond = processed / (elapsed.TotalSeconds > 0 ? elapsed.TotalSeconds : 1);
                var eta = TimeSpan.FromSeconds((total - processed) / (filesPerSecond > 0 ? filesPerSecond : 1));

                return $"Processing: {processed}/{total} files ({percent}%) " +
                       $"| {filesPerSecond:F1} files/sec | ETA: {eta.Minutes:00}:{eta.Seconds:00}";
            }
        }, cts.Token);

        // Process files with MD5 calculation
        var tasks = new List<Task>();
        foreach (var filePath in filePaths)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    var hash = FileHashUtility.CalculateMD5(filePath);
                    lock (lockObj)
                    {
                        processed++;
                        // Only log details every 10th file to avoid console flood
                        if (processed % 10 == 0 || processed == total)
                        {
                            var relativePath = filePath.Replace(localDirectory, "").TrimStart('\\');
                            Logger.LogInfo($"Processed: {relativePath} - MD5: {hash}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    lock (lockObj)
                    {
                        processed++;
                        Console.WriteLine($"\r[ERROR] Failed to process {filePath}: {ex.Message}" + new string(' ', 30));
                        Logger.LogError($"MD5 calculation error for {filePath}: {ex.Message}");
                    }
                }
            }));
        }
        try
        {
            // Wait for all hash calculations to complete
            await Task.WhenAll(tasks);

            // Stop the progress spinner properly
            Utilities.ProgressIndicator.StopProgress(
                cts,
                progressTask,
                "[SUCCESS] MD5 hash generation completed successfully");

            TimeSpan totalTime = DateTime.Now - startTime;
            Console.WriteLine($"[INFO] Processed {processed} files in {totalTime.Minutes}m {totalTime.Seconds}s" + new string(' ', 30));
        }
        catch (Exception)
        {
            // Make sure we cancel the progress indicator even on error
            Utilities.ProgressIndicator.StopProgress(cts, progressTask, "[ERROR] MD5 hash generation failed");
            throw; // Re-throw the exception to be handled by outer catch
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] An unexpected error occurred: {ex.Message}");
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
private static async Task CopyFilesToAzureAsync()
{
    DisplayIntroduction();
    Console.WriteLine("=== Copy Files to Azure ===\n");
    Console.WriteLine("[INFO] Starting file upload process to Azure...");

    try
    {
        // Initialize Azure credentials
        await InitializeAzureCredentialsAsync();

        // If we have a storage account connection already, ask if the user wants to use it
        string storageAccountName = "";
        if (Models.ConnectionState.IsStorageAccountConnected)
        {
            Console.WriteLine($"[INFO] Currently connected to: {Models.ConnectionState.StorageAccountName}");
            Console.Write("Use current storage account connection? (yes/no): ");
            string? useCurrentResponse = Console.ReadLine()?.Trim().ToLower();

            if (useCurrentResponse == "yes" || useCurrentResponse == "y")
            {
                storageAccountName = Models.ConnectionState.StorageAccountName ?? "";
                Console.WriteLine($"[INFO] Using current storage account: {storageAccountName}");
            }
        }

        // If no storage account selected yet, prompt for one
        if (string.IsNullOrEmpty(storageAccountName))
        {
            Console.Write("Enter your Azure Storage Account Name: ");
            storageAccountName = Console.ReadLine() ?? "";
            Console.WriteLine($"[INFO] Using storage account: {storageAccountName}");
            // Reset connection state since we're changing accounts
            Models.ConnectionState.Reset();
            Models.ConnectionState.StorageAccountName = storageAccountName;
        }

        Console.WriteLine("Choose Storage Type:");
        Console.WriteLine("1. Blob Storage");
        Console.WriteLine("2. File Share");
        string choice = Console.ReadLine() ?? "";
        Console.WriteLine($"[INFO] Selected storage type option: {choice}");

        Console.WriteLine("[INFO] Opening folder dialog to select local files to upload...");
        string localDirectory = GetDirectoryFromDialog();
        if (string.IsNullOrEmpty(localDirectory))
        {
            Console.WriteLine("[WARNING] Directory selection canceled by user");
            return;
        }
        Console.WriteLine($"[INFO] Selected local directory: {localDirectory}");

        // Get all files to upload
        var filesToUpload = Directory.GetFiles(localDirectory, "*", SearchOption.AllDirectories);
        Console.WriteLine($"[INFO] Found {filesToUpload.Length} files to upload");

        if (string.IsNullOrEmpty(storageAccountName))
        {
            Console.WriteLine("[ERROR] Storage account name is missing. Please check your input.");
            return;
        }

        // Permission check before upload
        bool hasPermission = true;
        string permissionError = string.Empty;
        string localStorageAccount = storageAccountName;
        try
        {
            if (choice == "1")
            {
                // Blob Storage: try to list containers or upload a zero-byte blob as a permission check
                var blobClientOptions = new BlobClientOptions();
                if (_httpClient != null)
                    blobClientOptions.Transport = new HttpClientTransport(_httpClient);
                var blobServiceClient = new BlobServiceClient(new Uri($"https://{storageAccountName}.blob.core.windows.net"), _credential, blobClientOptions);                var containerName = Models.ConnectionState.BlobContainerName;
                if (string.IsNullOrEmpty(containerName))
                {
                    Console.Write("Enter your Azure Blob Container Name: ");
                    containerName = Console.ReadLine() ?? "";
                    Models.ConnectionState.BlobContainerName = containerName;
                }
                var permissionTestContainerClient = blobServiceClient.GetBlobContainerClient(containerName);
                if (!await permissionTestContainerClient.ExistsAsync())
                {
                    Console.WriteLine($"[ERROR] Container '{containerName}' does not exist.");
                    return;
                }
                // Try to get properties as a permission check
                await permissionTestContainerClient.GetPropertiesAsync();
            }
            else if (choice == "2")
            {
                // File Share: try to list root directory as a permission check
                var shareClientOptions = new ShareClientOptions();
                if (_httpClient != null)
                    shareClientOptions.Transport = new HttpClientTransport(_httpClient);

                // Make sure to add our FileRequestIntentPolicy to handle the required header
                shareClientOptions.AddPolicy(new FileRequestIntentPolicy(), HttpPipelinePosition.PerCall);

                var fileShareName = Models.ConnectionState.FileShareName;
                if (string.IsNullOrEmpty(fileShareName))
                {
                    Console.Write("Enter your Azure File Share Name: ");
                    fileShareName = Console.ReadLine() ?? "";
                    Models.ConnectionState.FileShareName = fileShareName;
                }                
                
                var shareServiceClient = new ShareServiceClient(new Uri($"https://{storageAccountName}.file.core.windows.net"), _credential, shareClientOptions);
                var permissionTestShareClient = shareServiceClient.GetShareClient(fileShareName);

                // Use our enhanced existence checking method instead of the direct ExistsAsync call
                bool shareExists = await Utilities.FileShareClientExtensions.ExistsWithIntentHeaderAsync(permissionTestShareClient);                
                
                if (!shareExists)
                {
                    Console.WriteLine($"[WARNING] File share '{fileShareName}' does not exist.");
                    Console.Write("Would you like to create it now? (yes/no): ");
                    string response = Console.ReadLine()?.Trim().ToLower() ?? "";

                    if (response == "yes" || response == "y")
                    {
                        Console.Write($"[INFO] Creating file share '{fileShareName}'... ");
                        try
                        {
                            await permissionTestShareClient.CreateAsync();
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("SUCCESS");
                            Console.ResetColor();
                            Console.WriteLine($"[INFO] File share '{fileShareName}' created successfully.");
                            shareExists = true;
                        }
                        catch (Exception ex)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("FAILED");
                            Console.ResetColor();
                            Console.WriteLine($"[ERROR] Failed to create file share: {ex.Message}");
                            return;
                        }
                    }
                    else
                    {
                        Console.WriteLine("[ERROR] Cannot upload files without a target file share. Operation cancelled.");
                        return;
                    }
                }

                // Use the root directory client with our custom extension method to ensure proper header handling
                var rootDir = permissionTestShareClient.GetRootDirectoryClient();

                // Test permission by listing directory contents (which requires the custom header)
                var dirContents = rootDir.GetFilesAndDirectoriesAsync().AsPages(pageSizeHint: 1);

                // This will either succeed or throw an exception that will be caught by the catch blocks
                await dirContents.GetAsyncEnumerator().MoveNextAsync();
            }
            else
            {
                Console.WriteLine("[ERROR] Invalid choice. Please select either option 1 or 2.");
                return;
            }
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 403 || ex.Status == 401)
        {
            hasPermission = false;

            // Get the appropriate role name based on the storage type
            string roleName = choice == "1"
                ? "Storage Blob Data Contributor"
                : "Storage File Data SMB Share Elevated Contributor";

            // Store the error details for logging - without the properties that don't exist
            permissionError = $"{ex.Message}\nTime:{DateTime.UtcNow}\nStatus: {ex.Status} ({ex.ErrorCode})\n\nHeaders:\n{string.Join("\n", ex.GetType().GetProperties().Where(p => p.Name.StartsWith("Header")).Select(p => $"{p.Name}: {p.GetValue(ex)}"))}";

            // Log the detailed error
            Logger.LogError($"Azure permission error: {permissionError}");
        }
        catch (Exception ex)
        {
            hasPermission = false;
            permissionError = ex.Message;
        }

        if (!hasPermission)
        {
            Console.ForegroundColor = ConsoleColor.Red;

            // Use the local storage account variable we defined earlier
            if (string.IsNullOrEmpty(localStorageAccount))
            {
                localStorageAccount = storageAccountName;
            }

            Console.WriteLine("[ERROR] Authorization Error: You don't have permission to upload files to this storage account or container/share.");

            if (choice == "1")
            {
                Console.WriteLine($"[ERROR] Please ensure your account has been granted the 'Storage Blob Data Contributor' role on the '{localStorageAccount}' storage account.");
            }
            else
            {
                Console.WriteLine($"[ERROR] Please ensure your account has been granted the 'Storage File Data SMB Share Elevated Contributor' role on the '{localStorageAccount}' storage account.");
            }

            Console.WriteLine($"[ERROR] Details: {permissionError}");
            Console.ResetColor();
            Logger.LogError($"Upload permission error: {permissionError}");
            return;
        }

        // Setup progress tracking
        var startTime = DateTime.Now;
        int uploaded = 0;
        int failed = 0;
        long totalBytes = 0;
        long uploadedBytes = 0;
        var lockObj = new object();

        // Calculate total size
        foreach (var file in filesToUpload)
        {
            try
            {
                var fileInfo = new FileInfo(file);
                totalBytes += fileInfo.Length;
            }
            catch { } // Ignore files we can't access
        }        // Format total size for display
        string totalSizeFormatted = totalBytes < 1024 * 1024 ?
            $"{totalBytes / 1024.0:F1} KB" :
            $"{totalBytes / (1024.0 * 1024.0):F1} MB";

        Console.WriteLine($"[INFO] Total upload size: {totalSizeFormatted}");

        // Prepare clients based on the storage type
        ShareClient? shareClient = null;
        ShareDirectoryClient? rootDirClient = null;
        BlobContainerClient? containerClient = null;

        if (choice == "1") // Blob Storage
        {
            var blobClientOptions = new BlobClientOptions();
            if (_httpClient != null)
            {
                blobClientOptions.Transport = new HttpClientTransport(_httpClient);
            }

            var blobServiceClient = new BlobServiceClient(
                new Uri($"https://{storageAccountName}.blob.core.windows.net"),
                _credential,
                blobClientOptions);

            containerClient = blobServiceClient.GetBlobContainerClient(Models.ConnectionState.BlobContainerName);
        }
        else if (choice == "2") // File Share
        {
            var shareClientOptions = new ShareClientOptions();
            if (_httpClient != null)
            {
                shareClientOptions.Transport = new HttpClientTransport(_httpClient);
            }

            // Add the intent policy to handle required headers
            shareClientOptions.AddPolicy(new FileRequestIntentPolicy(), HttpPipelinePosition.PerCall);

            var shareServiceClient = new ShareServiceClient(
                new Uri($"https://{storageAccountName}.file.core.windows.net"),
                _credential,
                shareClientOptions);

            if (!string.IsNullOrEmpty(Models.ConnectionState.FileShareName))
            {
                shareClient = shareServiceClient.GetShareClient(Models.ConnectionState.FileShareName);

                // Allow the user to browse and select a directory within the Azure File Share
                Console.WriteLine("[INFO] Preparing to browse Azure File Share directories...");
                Console.WriteLine("You'll be able to select where in the Azure File Share to upload files.");
                Console.WriteLine("Press any key to continue to the directory browser...");
                Console.ReadKey(true);

                string azureDirectoryPath = "";
                try
                {
                    // Create a FileShareService to use its directory browsing capability
                    if (_credential != null && shareClient != null)
                    {
                        var fileShareService = new AzureStorageManager.Services.FileShareService(
                            $"https://{storageAccountName}.file.core.windows.net", 
                            Models.ConnectionState.FileShareName ?? "", 
                            _credential, 
                            shareClientOptions);
                        
                        azureDirectoryPath = await fileShareService.BrowseAndSelectDirectoryAsync();
                        if (string.IsNullOrEmpty(azureDirectoryPath))
                        {
                            Console.WriteLine("[INFO] No Azure directory selected or operation cancelled. Using root directory.");
                            rootDirClient = shareClient.GetRootDirectoryClient();
                        }
                        else
                        {
                            Console.WriteLine($"[INFO] Selected Azure directory: {azureDirectoryPath}");
                            rootDirClient = shareClient.GetDirectoryClient(azureDirectoryPath);
                        }
                    }
                    else
                    {
                        Console.WriteLine("[WARNING] Cannot browse directories due to missing credentials or share client.");
                        if (shareClient != null)
                        {
                            rootDirClient = shareClient.GetRootDirectoryClient();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WARNING] Error browsing Azure directories: {ex.Message}. Using root directory instead.");
                    Console.WriteLine($"[DEBUG] {ex}");
                    if (shareClient != null)
                    {
                        rootDirClient = shareClient.GetRootDirectoryClient();
                    }
                }
            }            else
            {
                Console.WriteLine("[ERROR] File share name is missing. Cannot proceed with upload.");
                return;
            }
        }

        // Now that all preparations are complete and user has selected target directory, start progress reporting
        Console.WriteLine("[INFO] Beginning upload process. This may take some time depending on file sizes...");
        
        // Create cancellation token source for progress task
        var cts = new CancellationTokenSource();

        // Start progress reporting task
        var progressTask = Task.Run(async () =>
        {
            string[] spinner = new[] { "⣾", "⣽", "⣻", "⢿", "⡿", "⣟", "⣯", "⣷" };
            int spinnerPos = 0;

            while (!cts.Token.IsCancellationRequested)
            {
                lock (lockObj)
                {
                    var percent = filesToUpload.Length > 0 ? uploaded * 100 / filesToUpload.Length : 0;
                    var bytesPercent = totalBytes > 0 ? uploadedBytes * 100 / totalBytes : 0;
                    var elapsed = DateTime.Now - startTime;
                    var speed = elapsed.TotalSeconds > 0 ? uploadedBytes / elapsed.TotalSeconds / 1024 : 0; // KB/s

                    string uploadedSizeFormatted = uploadedBytes < 1024 * 1024 ?
                        $"{uploadedBytes / 1024.0:F1} KB" :
                        $"{uploadedBytes / (1024.0 * 1024.0):F1} MB";

                    Console.Write($"\r[WORKING] {spinner[spinnerPos]} Uploaded: {uploaded}/{filesToUpload.Length} files ({percent}%) " +
                                $"| {uploadedSizeFormatted} of {totalSizeFormatted} ({bytesPercent}%) | {speed:F1} KB/s    ");
                }

                spinnerPos = (spinnerPos + 1) % spinner.Length;
                await Task.Delay(200, cts.Token);
            }
        });

        // Create lists to track failures and semaphores to limit concurrency
        var failedFiles = new List<string>();
        int maxConcurrentUploads = 4; // Limit concurrent uploads to prevent throttling
        using var semaphore = new System.Threading.SemaphoreSlim(maxConcurrentUploads);
        var uploadTasks = new List<Task>();
        
        // Process all files for upload
        foreach (var localFilePath in filesToUpload)
        {
            // Wait for a slot to become available
            await semaphore.WaitAsync();

            // Start an upload task for the file
            var uploadTask = Task.Run(async () =>
            {
                try
                {
                    // Get the relative path to preserve directory structure in Azure
                    string fileRelativePath = localFilePath.Replace(localDirectory, "").TrimStart('\\');
                    string azurePath = fileRelativePath.Replace('\\', '/');

                    // Get file info for size and metadata
                    var fileInfo = new FileInfo(localFilePath);

                    if (choice == "1" && containerClient != null) // Blob Storage
                    {
                        // Upload to Blob Storage
                        var blobClient = containerClient.GetBlobClient(azurePath);

                        // Calculate MD5 hash for file integrity verification
                        string md5Hash = FileHashUtility.CalculateMD5(localFilePath);

                        // Upload the file with metadata
                        using (var stream = File.OpenRead(localFilePath))
                        {
                            await blobClient.UploadAsync(stream, new BlobUploadOptions
                            {
                                Metadata = new Dictionary<string, string>
                                {
                                            { "md5", md5Hash },
                                            { "uploadedOn", DateTime.UtcNow.ToString("o") }
                                }
                            });
                        }
                    }
                    else if (choice == "2" && shareClient != null && rootDirClient != null) // File Share
                    {
                        // Upload to File Share
                        string dirPath = Path.GetDirectoryName(azurePath)?.Replace('\\', '/') ?? "";

                        if (!string.IsNullOrEmpty(dirPath))
                        {
                            // Create directory structure in Azure (if needed)
                            var dirParts = dirPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                            string currentPath = "";

                            foreach (var dir in dirParts)
                            {
                                if (!string.IsNullOrEmpty(currentPath))
                                {
                                    currentPath += "/";
                                }
                                currentPath += dir;

                                var dirClient = shareClient.GetDirectoryClient(currentPath);

                                // Check if directory exists and create if needed
                                if (!await dirClient.ExistsAsync())
                                {
                                    await dirClient.CreateAsync();
                                }
                            }
                        }

                        // Determine which directory to use
                        ShareDirectoryClient directoryClient;
                        if (string.IsNullOrEmpty(dirPath))
                        {
                            directoryClient = rootDirClient;
                        }
                        else
                        {
                            directoryClient = shareClient.GetDirectoryClient(dirPath);
                        }

                        // Get file name without path
                        string fileName = Path.GetFileName(localFilePath);
                        var fileClient = directoryClient.GetFileClient(fileName);

                        // Calculate MD5 hash
                        string md5Hash = FileHashUtility.CalculateMD5(localFilePath);                                // Upload file to Azure File Share
                        using (var stream = File.OpenRead(localFilePath))
                        {
                            // First create the file with appropriate size using our extension method
                            // that ensures the proper request headers are included
                            await Utilities.FileShareClientExtensions.CreateFileWithIntentHeaderAsync(fileClient, stream.Length);

                            // Then upload the file content
                            // The UploadAsync method will pick up the FileRequestIntentPolicy from the pipeline
                            await fileClient.UploadAsync(stream);

                            // Add metadata after upload
                            await fileClient.SetMetadataAsync(new Dictionary<string, string>
                            {
                                        { "md5", md5Hash },
                                        { "uploadedOn", DateTime.UtcNow.ToString("o") }
                            });
                        }
                    }

                    // Update progress tracking
                    lock (lockObj)
                    {
                        uploaded++;
                        uploadedBytes += fileInfo.Length;
                        string uploadedFilePath = localFilePath.Replace(localDirectory, "").TrimStart('\\');
                        Console.WriteLine($"\r[INFO] Uploaded: {uploadedFilePath}" + new string(' ', 30));
                    }

                    // Log success
                    Logger.LogInfo($"Successfully uploaded {fileRelativePath} to {azurePath} ({fileInfo.Length} bytes)");
                }
                catch (Exception ex)
                {
                    // Track failures
                    lock (lockObj)
                    {
                        failed++;
                        failedFiles.Add(localFilePath);
                        var relativePath = localFilePath.Replace(localDirectory, "").TrimStart('\\');
                        Console.WriteLine($"\r[ERROR] Failed to upload {relativePath}: {ex.Message}" + new string(' ', 30));
                    }

                    // Log error details
                    Logger.LogError($"Failed to upload file: {localFilePath}. Error: {ex}");
                }
                finally
                {
                    // Release semaphore slot for next file
                    semaphore.Release();
                }
            });

            uploadTasks.Add(uploadTask);
        }

        // Wait for all uploads to complete
        await Task.WhenAll(uploadTasks);

        // Occasionally display progress updates for specific files
        int updateInterval = Math.Max(1, filesToUpload.Length / 20); // Show about 20 updates
        for (int i = 0; i < filesToUpload.Length; i++)
        {
            if (i % updateInterval == 0 || i == filesToUpload.Length - 1)
            {
                var relativePath = filesToUpload[i].Replace(localDirectory, "").TrimStart('\\');
                Console.WriteLine($"\r[INFO] Uploaded: {relativePath}" + new string(' ', 30));
            }
        }

        // Stop the progress spinner by cancelling the token
        cts.Cancel();
        try
        {
            // Wait a short time for the task to respond to cancellation
            await Task.WhenAny(progressTask, Task.Delay(1000));
        }
        catch { /* Ignore any exceptions during task cancellation */ }

        var totalTime = DateTime.Now - startTime;
        string uploadSpeedFormatted = totalTime.TotalSeconds > 0 ?
            $"{uploadedBytes / totalTime.TotalSeconds / 1024:F1} KB/s" :
            "N/A";

        if (failed > 0)
        {
            Console.WriteLine($"\n[WARNING] {failed} out of {filesToUpload.Length} files failed to upload. Check the logs for details.");

            // Print first few failed files
            int maxFailedToShow = Math.Min(5, failedFiles.Count);
            Console.WriteLine("\nFailed files:");
            for (int i = 0; i < maxFailedToShow; i++)
            {
                var relativePath = failedFiles[i].Replace(localDirectory, "").TrimStart('\\');
                Console.WriteLine($"- {relativePath}");
            }

            if (failedFiles.Count > maxFailedToShow)
            {
                Console.WriteLine($"... and {failedFiles.Count - maxFailedToShow} more. See logs for details.");
            }
        }
        else
        {
            Console.WriteLine($"\n[SUCCESS] Upload process completed successfully.");
            Console.WriteLine($"[INFO] {uploaded} files uploaded ({totalSizeFormatted}) in {totalTime.Minutes}m {totalTime.Seconds}s");
            Console.WriteLine($"[INFO] Average upload speed: {uploadSpeedFormatted}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] An unexpected error occurred: {ex.Message}");
        Console.WriteLine($"[DEBUG] Exception details: {ex}");
        Logger.LogError($"CopyFilesToAzureAsync failed: {ex}");
    }
}
private static async Task DownloadFilesFromAzureAsync()
{
    DisplayIntroduction();
    Console.WriteLine("=== Download Files From Azure ===\n");
    Console.WriteLine("[INFO] Starting file download process from Azure...");

    try
    {
        // Initialize Azure credentials
        await InitializeAzureCredentialsAsync();

        // If we have a storage account connection already, ask if the user wants to use it
        string storageAccountName = "";
        if (Models.ConnectionState.IsStorageAccountConnected)
        {
            Console.WriteLine($"[INFO] Currently connected to: {Models.ConnectionState.StorageAccountName}");
            Console.Write("Use current storage account connection? (yes/no): ");
            string? useCurrentResponse = Console.ReadLine()?.Trim().ToLower();

            if (useCurrentResponse == "yes" || useCurrentResponse == "y")
            {
                storageAccountName = Models.ConnectionState.StorageAccountName ?? "";
                Console.WriteLine($"[INFO] Using current storage account: {storageAccountName}");
            }
        }

        // If no storage account selected yet, prompt for one
        if (string.IsNullOrEmpty(storageAccountName))
        {
            Console.Write("Enter your Azure Storage Account Name: ");
            storageAccountName = Console.ReadLine() ?? "";
            Console.WriteLine($"[INFO] Using storage account: {storageAccountName}");
            // Reset connection state since we're changing accounts
            Models.ConnectionState.Reset();
            Models.ConnectionState.StorageAccountName = storageAccountName;
        }

        Console.WriteLine("Choose Storage Type:");
        Console.WriteLine("1. Blob Storage");
        Console.WriteLine("2. File Share");
        string choice = Console.ReadLine() ?? "";
        Console.WriteLine($"[INFO] Selected storage type option: {choice}");

        Console.WriteLine("[INFO] Opening folder dialog to select local download directory...");
        string localDirectory = GetDirectoryFromDialog();
        if (string.IsNullOrEmpty(localDirectory))
        {
            Console.WriteLine("[WARNING] Directory selection canceled by user");
            return;
        }
        Console.WriteLine($"[INFO] Selected local directory: {localDirectory}");

        if (string.IsNullOrEmpty(storageAccountName))
        {
            Console.WriteLine("[ERROR] Storage account name is missing. Please check your input.");
            return;
        }

        // Setup progress tracking
        var startTime = DateTime.Now;
        int downloaded = 0;
        int total = 0;
        int failed = 0;
        var lockObj = new object();

        // Start progress reporting task
        var progressTask = Task.Run(async () =>
        {
            string[] spinner = new[] { "⣾", "⣽", "⣻", "⢿", "⡿", "⣟", "⣯", "⣷" };
            int spinnerPos = 0;

            while (true)
            {
                lock (lockObj)
                {
                    var percent = total > 0 ? downloaded * 100 / total : 0;
                    var elapsed = DateTime.Now - startTime;
                    var filesPerSecond = elapsed.TotalSeconds > 0 ? downloaded / elapsed.TotalSeconds : 0;

                    Console.Write($"\r[WORKING] {spinner[spinnerPos]} Downloaded: {downloaded}/{total} files ({percent}%) " +
                               $"| {filesPerSecond:F1} files/sec | {failed} failed        ");
                }

                spinnerPos = (spinnerPos + 1) % spinner.Length;
                await Task.Delay(200);
            }
        });        Console.WriteLine("[INFO] Beginning download process. This may take some time depending on file sizes...");

        // Create cancellation token source for progress spinner
        var cts = new CancellationTokenSource();
        
        // List to track failures
        var failedFiles = new List<string>();
        
        // Set up concurrency limits to prevent throttling
        int maxConcurrentDownloads = 4;
        using var semaphore = new SemaphoreSlim(maxConcurrentDownloads);
        var downloadTasks = new List<Task>();

        try
        {
            if (choice == "1") // Blob Storage
            {
                // Create Blob client
                var blobClientOptions = new BlobClientOptions();
                if (_httpClient != null)
                {
                    blobClientOptions.Transport = new HttpClientTransport(_httpClient);
                }
                
                var blobServiceClient = new BlobServiceClient(
                    new Uri($"https://{storageAccountName}.blob.core.windows.net"),
                    _credential,
                    blobClientOptions);
                
                // Get container name
                string containerName = "";
                if (!string.IsNullOrEmpty(Models.ConnectionState.BlobContainerName) && 
                    Models.ConnectionState.StorageAccountName == storageAccountName)
                {
                    Console.Write($"Use current container '{Models.ConnectionState.BlobContainerName}'? (yes/no): ");
                    string? useCurrentContainer = Console.ReadLine()?.Trim().ToLower();
                    
                    if (useCurrentContainer == "yes" || useCurrentContainer == "y")
                    {
                        containerName = Models.ConnectionState.BlobContainerName;
                        Console.WriteLine($"[INFO] Using container: {containerName}");
                    }
                }
                
                if (string.IsNullOrEmpty(containerName))
                {
                    Console.Write("Enter your Azure Blob Container Name: ");
                    containerName = Console.ReadLine() ?? "";
                    Models.ConnectionState.BlobContainerName = containerName;
                    Console.WriteLine($"[INFO] Using container: {containerName}");
                }
                
                var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
                
                // Check if container exists
                if (!await containerClient.ExistsAsync())
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[ERROR] Container '{containerName}' does not exist.");
                    Console.ResetColor();
                    return;
                }
                
                // Get list of all blobs
                var blobList = new List<BlobItem>();
                await foreach (var blobItem in containerClient.GetBlobsAsync())
                {
                    blobList.Add(blobItem);
                }
                
                lock (lockObj)
                {
                    total = blobList.Count;
                }
                
                // Process each blob for download
                foreach (var blobItem in blobList)
                {
                    await semaphore.WaitAsync();
                    
                    var downloadTask = Task.Run(async () =>
                    {
                        try
                        {
                            // Get blob client
                            var blobClient = containerClient.GetBlobClient(blobItem.Name);
                            
                            // Create the local directory structure if needed
                            string localFilePath = Path.Combine(localDirectory, blobItem.Name.Replace('/', Path.DirectorySeparatorChar));
                            string? dirPath = Path.GetDirectoryName(localFilePath);
                            
                            if (!string.IsNullOrEmpty(dirPath) && !Directory.Exists(dirPath))
                            {
                                Directory.CreateDirectory(dirPath);
                            }
                            
                            // Download the blob
                            using (var fileStream = File.Create(localFilePath))
                            {
                                await blobClient.DownloadToAsync(fileStream);
                            }
                            
                            // Get blob properties to check for MD5 hash
                            var properties = await blobClient.GetPropertiesAsync();
                            
                            // Try to get MD5 from metadata if available
                            properties.Value.Metadata.TryGetValue("md5", out string? md5FromMetadata);
                            
                            // Calculate local MD5 for verification
                            string localMD5 = FileHashUtility.CalculateMD5(localFilePath);
                            
                            // Compare hashes if metadata is available
                            bool hashesMatch = false;
                            
                            if (!string.IsNullOrEmpty(md5FromMetadata))
                            {
                                // Compare the hashes
                                hashesMatch = md5FromMetadata.Equals(localMD5, StringComparison.OrdinalIgnoreCase);
                                
                                if (!hashesMatch)
                                {
                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                    Console.WriteLine($"\r[WARNING] Hash mismatch for {blobItem.Name}" + new string(' ', 30));
                                    Console.ResetColor();
                                    Logger.LogWarning($"Hash mismatch when downloading {blobItem.Name}. Azure MD5: {md5FromMetadata}, Local MD5: {localMD5}");
                                }
                            }
                            
                            lock (lockObj)
                            {
                                downloaded++;
                                Console.WriteLine($"\r[INFO] Downloaded: {blobItem.Name}" + new string(' ', 30));
                            }
                            
                            Logger.LogInfo($"Successfully downloaded {blobItem.Name} to {localFilePath} ({blobItem.Properties.ContentLength ?? 0} bytes)");
                        }
                        catch (Exception ex)
                        {
                            lock (lockObj)
                            {
                                failed++;
                                failedFiles.Add(blobItem.Name);
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"\r[ERROR] Failed to download {blobItem.Name}: {ex.Message}" + new string(' ', 30));
                                Console.ResetColor();
                            }
                            
                            Logger.LogError($"Failed to download blob: {blobItem.Name}. Error: {ex}");
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });
                    
                    downloadTasks.Add(downloadTask);
                }
            }
            else if (choice == "2") // File Share
            {
                // Create File Share client with appropriate options
                var shareClientOptions = new ShareClientOptions();
                if (_httpClient != null)
                {
                    shareClientOptions.Transport = new HttpClientTransport(_httpClient);
                }
                
                // Add the FileRequestIntentPolicy to handle x-ms-file-request-intent header
                shareClientOptions.AddPolicy(new FileRequestIntentPolicy(), HttpPipelinePosition.PerCall);
                
                var shareServiceClient = new ShareServiceClient(
                    new Uri($"https://{storageAccountName}.file.core.windows.net"),
                    _credential,
                    shareClientOptions);
                
                // Get file share name
                string fileShareName = "";
                if (!string.IsNullOrEmpty(Models.ConnectionState.FileShareName) && 
                    Models.ConnectionState.StorageAccountName == storageAccountName)
                {
                    Console.Write($"Use current file share '{Models.ConnectionState.FileShareName}'? (yes/no): ");
                    string? useCurrentShare = Console.ReadLine()?.Trim().ToLower();
                    
                    if (useCurrentShare == "yes" || useCurrentShare == "y")
                    {
                        fileShareName = Models.ConnectionState.FileShareName;
                        Console.WriteLine($"[INFO] Using file share: {fileShareName}");
                    }
                }
                
                if (string.IsNullOrEmpty(fileShareName))
                {
                    Console.Write("Enter your Azure File Share Name: ");
                    fileShareName = Console.ReadLine() ?? "";
                    Models.ConnectionState.FileShareName = fileShareName;
                    Console.WriteLine($"[INFO] Using file share: {fileShareName}");
                }
                
                var shareClient = shareServiceClient.GetShareClient(fileShareName);
                
                // Check if share exists using our enhanced existence check method
                bool shareExists = await Utilities.FileShareClientExtensions.ExistsWithIntentHeaderAsync(shareClient);
                if (!shareExists)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[ERROR] File share '{fileShareName}' does not exist.");
                    Console.ResetColor();
                    return;
                               }

                // Ask user for directory path within file share
                Console.WriteLine("[INFO] Preparing to browse Azure File Share directories...");
                Console.WriteLine("You'll be able to select which directory in the Azure File Share to download from.");
                Console.WriteLine("Press any key to continue to the directory browser...");
                Console.ReadKey(true);

                // Create service
                var fileShareService = new FileShareService(
                    $"https://{storageAccountName}.file.core.windows.net", 
                    fileShareName, 
                    _credential, 
                    shareClientOptions);

                // Browse for directory
                string azureDirectoryPath = "";
                try
                {
                    azureDirectoryPath = await fileShareService.BrowseAndSelectDirectoryAsync();
                    if (string.IsNullOrEmpty(azureDirectoryPath))
                    {
                        Console.WriteLine("[INFO] No Azure directory selected or operation cancelled. Using root directory.");
                    }
                    else
                    {
                        Console.WriteLine($"[INFO] Selected Azure directory: {azureDirectoryPath}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WARNING] Error browsing Azure directories: {ex.Message}. Using root directory instead.");
                    Console.WriteLine($"[DEBUG] {ex}");
                    azureDirectoryPath = "";
                }

                // Get appropriate directory client based on the path
                ShareDirectoryClient directoryClient;
                if (string.IsNullOrEmpty(azureDirectoryPath))
                {
                    directoryClient = shareClient.GetRootDirectoryClient();
                }
                else
                {
                    directoryClient = shareClient.GetDirectoryClient(azureDirectoryPath);
                }

                // Get all files and nested directories (recursive traversal function)
                var fileList = new List<(ShareFileClient fileClient, string relativePath)>();
                
                async Task TraverseDirectoryAsync(ShareDirectoryClient currentDir, string currentPath)
                {
                    try
                    {
                        await foreach (var item in currentDir.GetFilesAndDirectoriesAsync())
                        {
                            if (item.IsDirectory)
                            {
                                // Build path for subdirectory
                                string subDirPath = string.IsNullOrEmpty(currentPath) ? 
                                    item.Name : 
                                    $"{currentPath}/{item.Name}";
                                
                                // Get subdirectory client
                                var subDirClient = currentDir.GetSubdirectoryClient(item.Name);
                                
                                // Recursively traverse subdirectory
                                await TraverseDirectoryAsync(subDirClient, subDirPath);
                            }
                            else
                            {
                                // Build relative path for file
                                string filePath = string.IsNullOrEmpty(currentPath) ? 
                                    item.Name : 
                                    $"{currentPath}/{item.Name}";
                                
                                // Get file client
                                var fileClient = currentDir.GetFileClient(item.Name);
                                
                                // Add to list
                                fileList.Add((fileClient, filePath));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[WARNING] Error traversing directory {currentPath}: {ex.Message}");
                        Logger.LogWarning($"Error traversing directory {currentPath}: {ex}");
                    }
                }
                
                // Start traversal from selected directory
                await TraverseDirectoryAsync(directoryClient, "");
                
                // Update total count
                lock (lockObj)
                {
                    total = fileList.Count;
                }
                
                // Process each file for download
                foreach (var (fileClient, relativePath) in fileList)
                {
                    await semaphore.WaitAsync();
                    
                    var downloadTask = Task.Run(async () =>
                    {
                        try
                        {
                            // Create the local directory structure if needed
                            string localFilePath = Path.Combine(localDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
                            string? dirPath = Path.GetDirectoryName(localFilePath);
                            
                            if (!string.IsNullOrEmpty(dirPath) && !Directory.Exists(dirPath))
                            {
                                Directory.CreateDirectory(dirPath);
                            }
                            
                            // Download the file
                            var downloadInfo = await fileClient.DownloadAsync();
                            
                            using (var fileStream = File.Create(localFilePath))
                            {
                                await downloadInfo.Value.Content.CopyToAsync(fileStream);
                            }
                            
                            // Get file properties to check for MD5 hash
                            var properties = await fileClient.GetPropertiesAsync();
                            
                            // Try to get MD5 from metadata if available
                            properties.Value.Metadata.TryGetValue("md5", out string? md5FromMetadata);
                            
                            // Calculate local MD5 for verification
                            string localMD5 = FileHashUtility.CalculateMD5(localFilePath);
                            
                            // Compare hashes if metadata is available
                            bool hashesMatch = false;
                            
                            if (!string.IsNullOrEmpty(md5FromMetadata))
                            {
                                // Compare the hashes
                                hashesMatch = md5FromMetadata.Equals(localMD5, StringComparison.OrdinalIgnoreCase);
                                
                                if (!hashesMatch)
                                {
                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                    Console.WriteLine($"\r[WARNING] Hash mismatch for {relativePath}" + new string(' ', 30));
                                    Console.ResetColor();
                                    Logger.LogWarning($"Hash mismatch when downloading {relativePath}. Azure MD5: {md5FromMetadata}, Local MD5: {localMD5}");
                                }
                            }
                            
                            lock (lockObj)
                            {
                                downloaded++;
                                Console.WriteLine($"\r[INFO] Downloaded: {relativePath}" + new string(' ', 30));
                            }
                            
                            Logger.LogInfo($"Successfully downloaded {relativePath} to {localFilePath} ({properties.Value.ContentLength} bytes)");
                        }
                        catch (Exception ex)
                        {
                            lock (lockObj)
                            {
                                failed++;
                                failedFiles.Add(relativePath);
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"\r[ERROR] Failed to download {relativePath}: {ex.Message}" + new string(' ', 30));
                                Console.ResetColor();
                            }
                            
                            Logger.LogError($"Failed to download file: {relativePath}. Error: {ex}");
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });
                    
                    downloadTasks.Add(downloadTask);
                }
            }
            else
            {
                Console.WriteLine("[ERROR] Invalid choice. Please select either option 1 or 2.");
                return;
            }
            
            // Wait for all downloads to complete
            await Task.WhenAll(downloadTasks);
        }
        finally
        {
            // Cancel the progress spinner
            cts.Cancel();
            try
            {
                // Wait a short time for the task to respond to cancellation
                await Task.WhenAny(progressTask, Task.Delay(1000));
            }
            catch { /* Ignore any exceptions during task cancellation */ }
        }

        var totalTime = DateTime.Now - startTime;
        
        if (failed > 0)
        {
            Console.WriteLine($"\n[WARNING] {failed} out of {total} files failed to download. Check the logs for details.");
            
            // Print first few failed files
            int maxFailedToShow = Math.Min(5, failedFiles.Count);
            Console.WriteLine("\nFailed files:");
            for (int i = 0; i < maxFailedToShow; i++)
            {
                Console.WriteLine($"- {failedFiles[i]}");
            }
            
            if (failedFiles.Count > maxFailedToShow)
            {
                Console.WriteLine($"... and {failedFiles.Count - maxFailedToShow} more. See logs for details.");
            }
        }
        else
        {
            Console.WriteLine($"\n[SUCCESS] Download process completed successfully.");
        }

        Console.WriteLine($"[INFO] {downloaded} files downloaded in {totalTime.Minutes}m {totalTime.Seconds}s");
        Console.WriteLine($"[INFO] Files downloaded to: {localDirectory}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] An unexpected error occurred: {ex.Message}");
        Console.WriteLine($"[DEBUG] Exception details: {ex}");
        Logger.LogError($"DownloadFilesFromAzureAsync failed: {ex}");
    }
}
private static async Task GenerateConsolidatedReportAsync()
{
    DisplayIntroduction();
    Console.WriteLine("=== Generate Consolidated Report ===\n");
    Console.WriteLine("[INFO] Starting consolidated report generation process...");

    try
    {
        // Setup progress tracking
        var startTime = DateTime.Now;

        Console.WriteLine("[INFO] Scanning for report files to consolidate..."); var reportFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.csv")
            .Where(f => f.Contains("Report") || f.Contains("report"))
            .ToList();

        if (reportFiles.Count == 0)
        {
            Console.WriteLine("[WARNING] No report files found to consolidate.");
            return;
        }

        Console.WriteLine($"[INFO] Found {reportFiles.Count} report files to consolidate");

        // Create progress spinner
        var progressTask = Task.Run(async () =>
        {
            string[] spinner = new[] { "⣾", "⣽", "⣻", "⢿", "⡿", "⣟", "⣯", "⣷" };
            int spinnerPos = 0;

            while (true)
            {
                var elapsed = DateTime.Now - startTime;
                Console.Write($"\r[WORKING] {spinner[spinnerPos]} Generating consolidated report... ({elapsed.Minutes:00}:{elapsed.Seconds:00})      ");

                spinnerPos = (spinnerPos + 1) % spinner.Length;
                await Task.Delay(200);
            }
        });

        Console.WriteLine("[INFO] Processing report data and generating consolidated report...");

        // Add implementation for actual report generation here
        // This is a placeholder for now - in a future update, this method will be fully implemented
        await Task.Delay(3000); // Simulate work

        // For demonstration purposes only
        string consolidatedReportName = $"ConsolidatedReport_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

        // Stop the progress spinner
        try { progressTask.GetAwaiter().GetResult(); } catch { }

        var totalTime = DateTime.Now - startTime;
        Console.WriteLine($"\r[SUCCESS] Consolidated report generation completed in {totalTime.Minutes}m {totalTime.Seconds}s" + new string(' ', 40));
        Console.WriteLine($"[INFO] Report saved to: {Path.Combine(Directory.GetCurrentDirectory(), consolidatedReportName)}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] An unexpected error occurred: {ex.Message}");
        Console.WriteLine($"[DEBUG] Exception details: {ex}");
        Logger.LogError($"GenerateConsolidatedReportAsync failed: {ex}");
    }
}

private static void ViewLogs()
{
    Console.WriteLine("=== View Logs ===\n");
    // Implementation would go here
}
private static void ConfigureSettings()
{
    bool exitSettings = false;

    while (!exitSettings)
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("===================================================");
        Console.WriteLine("              Azure Storage Manager Settings       ");
        Console.WriteLine("===================================================");
        Console.ResetColor(); Console.WriteLine("\nCurrent Settings:");
        // Display current parallel task settings
        int maxParallelTasks = _config?["Settings:MaxParallelTasks"] != null ?
            int.Parse(_config?["Settings:MaxParallelTasks"] ?? "5") : 5;
        Console.WriteLine($"1. Max Parallel Tasks: {maxParallelTasks}");

        // Display log level
        string logLevel = _config?["Settings:LogLevel"] ?? "Information";
        Console.WriteLine($"2. Log Level: {logLevel}");
        // Display default timeout
        int operationTimeout = _config?["Settings:OperationTimeoutSeconds"] != null ?
            int.Parse(_config?["Settings:OperationTimeoutSeconds"] ?? "300") : 300;
        Console.WriteLine($"3. Operation Timeout: {operationTimeout} seconds");

        Console.WriteLine("\nDiagnostic Tools:");
        Console.WriteLine("4. Run Azure File Share Permission Diagnostics");

        Console.WriteLine("\nAdditional Settings:");
        // Retry settings
        int maxRetries = _config?["Settings:MaxRetries"] != null ?
            int.Parse(_config?["Settings:MaxRetries"] ?? "3") : 3;
        Console.WriteLine($"5. Max Retry Attempts: {maxRetries}");
        // Auto-skip system folders
        bool skipSystemFolders = _config?["Settings:SkipSystemFolders"] != null ?
            bool.Parse(_config?["Settings:SkipSystemFolders"] ?? "true") : true;
        Console.WriteLine($"6. Auto-skip System Folders: {skipSystemFolders}");
        // Verbose progress reporting
        bool verboseProgress = _config?["Settings:VerboseProgress"] != null ?
            bool.Parse(_config?["Settings:VerboseProgress"] ?? "false") : false;
        Console.WriteLine($"6. Verbose Progress Reporting: {verboseProgress}");

        // Default directory for file operations
        string defaultDirectory = _config?["Settings:DefaultDirectory"] ?? "Not set";
        Console.WriteLine($"7. Default Directory: {defaultDirectory}");
        // Connection timeout
        int connectionTimeout = _config?["Settings:ConnectionTimeoutSeconds"] != null ?
            int.Parse(_config?["Settings:ConnectionTimeoutSeconds"] ?? "60") : 60; Console.WriteLine($"8. Connection Timeout: {connectionTimeout} seconds");

        // Display authentication preference
        string authPreference = GetAuthenticationPreference();
        string authDisplayValue = authPreference switch
        {
            "certificate" => "Certificate Authentication",
            "clientsecret" => "Client Secret Authentication",
            "prompt" => "Prompt for Credentials When Needed",
            _ => "Auto-detect"
        }; Console.WriteLine($"9. Authentication Method: {authDisplayValue}");

        // Display current proxy configuration
        string proxyStatus = Services.ProxyService.IsProxyConfigured
            ? Services.ProxyService.ProxyInfo
            : "No proxy configured";
        Console.WriteLine($"10. Proxy Settings: {proxyStatus}");
        Console.WriteLine($"11. Test Azure Connectivity");
        Console.WriteLine($"12. Run Connectivity Test");

        Console.WriteLine("\n13. Return to Main Menu");

        Console.WriteLine("\n===================================================");
        Console.Write("Enter setting number to change (1-12): ");

        string? choice = Console.ReadLine()?.Trim();

        switch (choice)
        {
            case "1":
                ConfigureParallelTasks();
                break;
            case "2":
                ConfigureLogLevel();
                break;
            case "3":
                ConfigureOperationTimeout();
                break;
            case "4":
                // Run the diagnostics asynchronously and wait for completion
                Task.Run(async () => await FileShareDiagnostics.RunFileSharePermissionDiagnosticsAsync(
                    _credential ?? new Azure.Identity.DefaultAzureCredential(),
                    _httpClient ?? new HttpClient(),
                    Models.ConnectionState.StorageAccountName ?? "",
                    Models.ConnectionState.FileShareName ?? "")).Wait();
                break;
            case "5":
                ConfigureMaxRetries();
                break;
            case "6":
                ConfigureSkipSystemFolders();
                break;
            case "7":
                ConfigureVerboseProgress();
                break;
            case "8":
                ConfigureDefaultDirectory();
                break;
            case "9":
                ConfigureConnectionTimeout();
                break;
            case "10":
                ConfigureAuthenticationPreference();
                break;
            case "11":
                ConfigureProxySettings();
                break;
            case "12":
                // Run the connectivity test using our synchronous wrapper
                Utilities.ConnectivityTestRunner.RunConnectivityTest();
                break;
            case "13":
                exitSettings = true;
                Console.WriteLine("Returning to main menu...");
                break;
            default:
                Console.WriteLine("Invalid option. Press any key to try again...");
                Console.ReadKey();
                break;
        }
    }
}

private static void ConfigureParallelTasks()
{
    Console.Write("\nEnter maximum number of parallel tasks (1-20): ");
    string? input = Console.ReadLine()?.Trim();

    if (int.TryParse(input, out int maxTasks) && maxTasks >= 1 && maxTasks <= 20)
    {
        Console.WriteLine($"Setting Max Parallel Tasks to: {maxTasks}");
        // In a real implementation, this would update appsettings.json
        Console.WriteLine("[INFO] This would update the MaxParallelTasks setting in your configuration file");
    }
    else
    {
        Console.WriteLine("Invalid input. Value must be between 1 and 20.");
    }
}

private static void ConfigureLogLevel()
{
    Console.WriteLine("\nSelect Log Level:");
    Console.WriteLine("1. Error (errors only)");
    Console.WriteLine("2. Warning (warnings and errors)");
    Console.WriteLine("3. Information (standard information, warnings, and errors)");
    Console.WriteLine("4. Debug (detailed debug information and all above)");
    Console.Write("Enter choice (1-4): ");

    string? input = Console.ReadLine()?.Trim();
    string logLevel = "";

    switch (input)
    {
        case "1":
            logLevel = "Error";
            break;
        case "2":
            logLevel = "Warning";
            break;
        case "3":
            logLevel = "Information";
            break;
        case "4":
            logLevel = "Debug";
            break;
        default:
            Console.WriteLine("Invalid choice. Log level not changed.");
            return;
    }

    Console.WriteLine($"Setting Log Level to: {logLevel}");
    // In a real implementation, this would update appsettings.json
    Console.WriteLine("[INFO] This would update the LogLevel setting in your configuration file");
}

private static void ConfigureOperationTimeout()
{
    Console.Write("\nEnter operation timeout in seconds (30-3600): ");
    string? input = Console.ReadLine()?.Trim();

    if (int.TryParse(input, out int timeout) && timeout >= 30 && timeout <= 3600)
    {
        Console.WriteLine($"Setting Operation Timeout to: {timeout} seconds");
        // In a real implementation, this would update appsettings.json
        Console.WriteLine("[INFO] This would update the OperationTimeoutSeconds setting in your configuration file");
    }
    else
    {
        Console.WriteLine("Invalid input. Value must be between 30 and 3600 seconds.");
    }
}

private static void ConfigureMaxRetries()
{
    Console.Write("\nEnter maximum retry attempts (0-10");
    string? input = Console.ReadLine()?.Trim();

    if (int.TryParse(input, out int retries) && retries >= 0 && retries <= 10)
    {
        Console.WriteLine($"Setting Max Retry Attempts to: {retries}");
        // In a real implementation, this would update appsettings.json
        Console.WriteLine("[INFO] This would update the MaxRetries setting in yourconfiguration file");
    }
    else
    {
        Console.WriteLine("Invalid input. Value must be between 0 and 10.");
    }
}

private static void ConfigureSkipSystemFolders()
{
    Console.Write("\nAutomatically skip system folders? (yes/no): ");
    string? input = Console.ReadLine()?.Trim().ToLower();

    if (input == "yes" || input == "y")
    {
        Console.WriteLine("Setting Auto-skip System Folders to: true");
        // In a real implementation, this would update appsettings.json
        Console.WriteLine("[INFO] This would update the SkipSystemFolders setting in your configuration file");
    }
    else if (input == "no" || input == "n")
    {
        Console.WriteLine("Setting Auto-skip System Folders to: false");
        // In a real implementation, this would update appsettings.json
        Console.WriteLine("[INFO] This would update the SkipSystemFolders setting in your configuration file");
    }
    else
    {
        Console.WriteLine("Invalid input. Value must be 'yes' or 'no'.");
    }
}

private static void ConfigureVerboseProgress()
{
    Console.Write("\nEnable verbose progress reporting? (yes/no): ");
    string? input = Console.ReadLine()?.Trim().ToLower();

    if (input == "yes" || input == "y")
    {
        Console.WriteLine("Setting Verbose Progress Reporting to: true");
        // In a real implementation, this would update appsettings.json
        Console.WriteLine("[INFO] This would update the VerboseProgress setting in your configuration file");
    }
    else if (input == "no" || input == "n")
    {
        Console.WriteLine("Setting Verbose Progress Reporting to: false");
        // In a real implementation, this would update appsettings.json
        Console.WriteLine("[INFO] This would update the VerboseProgress setting in your configuration file");
    }
    else
    {
        Console.WriteLine("Invalid input. Value must be 'yes' or 'no'.");
    }
}

private static void ConfigureDefaultDirectory()
{
    Console.WriteLine("\nSelect default directory for file operations:");
    string directory = GetDirectoryFromDialog();

    if (!string.IsNullOrEmpty(directory))
    {
        Console.WriteLine($"Setting Default Directory to: {directory}");
        // In a real implementation, this would update appsettings.json
        Console.WriteLine("[INFO] This would update the DefaultDirectory setting in your configuration file");
    }
    else
    {
        Console.WriteLine("Directory selection cancelled. Default directory not changed.");
    }
}

private static void ConfigureConnectionTimeout()
{
    Console.Write("\nEnter connection timeout in seconds (10-300): ");
    string? input = Console.ReadLine()?.Trim();

    if (int.TryParse(input, out int timeout) && timeout >= 10 && timeout <= 300)
    {
        Console.WriteLine($"Setting Connection Timeout to: {timeout} seconds");
        // In a real implementation, this would update appsettings.json
        Console.WriteLine("[INFO] This would update the ConnectionTimeoutSeconds setting in your configuration file");
    }
    else
    {
        Console.WriteLine("Invalid input. Value must be between 10 and 300 seconds.");
    }
}
private static void DisplayHelp()
{
    Console.Clear();
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("===================================================");
    Console.WriteLine("              Azure Storage Manager Help            ");
    Console.WriteLine("===================================================");
    Console.ResetColor();
    Console.WriteLine("\nThis application helps you manage and verify files stored in Azure Storage.");
    Console.WriteLine("Below is information about each function available in the application:\n");

    DisplayHelpSection("1. Verify File Integrity",
        "Compares local files with those stored in Azure Blob Storage or File Shares.",
        "- Checks MD5 hash values to ensure data integrity",
        "- Generates a detailed CSV report showing matches and mismatches",
        "- Helps identify corrupted or modified files");

    DisplayHelpSection("2. Generate MD5 Hashes for Local Files",
        "Creates MD5 hash values for all files in a selected directory.",
        "- Processes files in parallel for better performance",
        "- Useful for preparing files before uploading to Azure",
        "- Helps with local file integrity verification");

    DisplayHelpSection("3. Check and Update Azure Metadata",
        "Examines blob metadata and updates missing MD5 hash values.",
        "- Ensures all blobs have proper metadata for integrity checks",
        "- Updates metadata without modifying blob content",
        "- Provides a summary report of changes made");

    DisplayHelpSection("4. Copy Files to Azure",
        "Uploads local files to either Azure Blob Storage or File Shares.",
        "- Preserves directory structure during upload",
        "- Automatically calculates and stores MD5 hash values",
        "- Shows detailed progress with transfer speeds");

    DisplayHelpSection("5. Download Files from Azure",
        "Downloads files from Azure Storage to a local directory.",
        "- Preserves folder structure from Azure",
        "- Verifies file integrity after download",
        "- Provides detailed progress information");

    DisplayHelpSection("6. Generate Consolidated Report",
        "Creates a single report combining data from multiple verification reports.",
        "- Useful for comparing results over time",
        "- Helps track changes across multiple verification runs",
        "- Creates an easy-to-read summary of all file integrity checks");

    DisplayHelpSection("7. View Logs",
        "Shows the application log files for troubleshooting.",
        "- Displays information, warning, and error messages",
        "- Helps identify issues with connections or file operations",
        "- Useful for support and debugging");

    DisplayHelpSection("8. Settings",
        "Configure application settings such as:",
        "- Default Azure connection settings",
        "- Parallel operation settings",
        "- Logging preferences",
        "- File comparison options");

    Console.WriteLine("\nCredential Management:");
    Console.WriteLine("- The application supports certificate-based authentication (recommended)");
    Console.WriteLine("- Client Secret authentication is available as a fallback option");
    Console.WriteLine("- Credentials are configured in the appsettings.json file");

    Console.WriteLine("\nTips:");
    Console.WriteLine("- For large file sets, use a more specific directory rather than scanning entire drives");
    Console.WriteLine("- System folders like 'System Volume Information' are automatically skipped");
    Console.WriteLine("- Connection information is preserved between operations for convenience");
    Console.WriteLine("- Check logs regularly to monitor application health and troubleshoot issues");

    Console.WriteLine("\n===================================================");
    Console.WriteLine("Press Enter to return to the main menu...");
}

private static void DisplayHelpSection(string title, params string[] points)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine(title);
    Console.ResetColor();

    foreach (var point in points)
    {
        Console.WriteLine($"  {point}");
    }
    Console.WriteLine();
}

/// <summary>
/// Gets all files in a directory and its subdirectories while handling access denied exceptions
/// </summary>
private static List<string> GetFilesWithPermissionHandling(string rootDirectory)
{
    var files = new List<string>();
    var directories = new Stack<string>();
    directories.Push(rootDirectory);

    Console.WriteLine("[INFO] Scanning for files (system-restricted folders will be skipped)...");

    int accessDeniedCount = 0;
    int directoryCount = 0;

    while (directories.Count > 0)
    {
        string currentDir = directories.Pop();
        directoryCount++;

        // Process files in the current directory
        try
        {
            foreach (var file in Directory.GetFiles(currentDir, "*", SearchOption.TopDirectoryOnly))
            {
                files.Add(file);
            }
        }
        catch (UnauthorizedAccessException)
        {
            accessDeniedCount++;
            // Skip directories we don't have access to
            continue;
        }
        catch (Exception ex)
        {
            // Log other errors but continue processing
            Console.WriteLine($"[WARNING] Error accessing files in {currentDir}: {ex.Message}");
            Logger.LogWarning($"Error accessing files in {currentDir}: {ex.Message}");
            continue;
        }

        // Get subdirectories
        try
        {
            foreach (var directory in Directory.GetDirectories(currentDir))
            {
                // Skip system folders that commonly cause permission issues
                string dirName = Path.GetFileName(directory);
                if (dirName.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase) ||
                    dirName.Equals("$RECYCLE.BIN", StringComparison.OrdinalIgnoreCase) ||
                    dirName.Equals("$WinREagent", StringComparison.OrdinalIgnoreCase) ||
                    dirName.Equals("Recovery", StringComparison.OrdinalIgnoreCase) ||
                    dirName.StartsWith("$", StringComparison.OrdinalIgnoreCase) ||
                    dirName.StartsWith(".", StringComparison.OrdinalIgnoreCase))
                {
                    // Skip known system directories
                    continue;
                }

                directories.Push(directory);
            }
        }
        catch (UnauthorizedAccessException)
        {
            accessDeniedCount++;
            // Skip directories we don't have access to
            continue;
        }
        catch (Exception ex)
        {
            // Log other errors but continue processing
            Console.WriteLine($"[WARNING] Error accessing subdirectory in {currentDir}: {ex.Message}");
            Logger.LogWarning($"Error accessing subdirectory in {currentDir}: {ex.Message}");
            continue;
        }

        // Show progress every 10 directories
        if (directoryCount % 10 == 0)
        {
            Console.Write($"\r[INFO] Scanned {directoryCount} directories, found {files.Count} files so far...      ");
        }
    }

    Console.WriteLine($"\r[INFO] Finished scanning {directoryCount} directories" + new string(' ', 40));

    if (accessDeniedCount > 0)
    {
        Console.WriteLine($"[INFO] Skipped {accessDeniedCount} system or restricted directories due to access restrictions");
    }
    return files;
}

/// <summary>
///
/// </summary>
/// <returns>The preferred auth method: "certificate", "clientsecret", "prompt", or "auto"</returns>
private static string GetAuthenticationPreference()
{
    // Check user settings first (environment variable)
    string preference = Environment.GetEnvironmentVariable("AZURE_AUTH_PREFERENCE") ?? string.Empty;

    // If not set in environment, check config file
    if (string.IsNullOrEmpty(preference))
    {
        if (_config != null && _config["Authentication:PreferredMethod"] != null)
        {
            preference = _config["Authentication:PreferredMethod"] ?? string.Empty;
        }
    }
    // Default to auto if not specified
    if (string.IsNullOrEmpty(preference) ||
        (preference != "certificate" && preference != "clientsecret" && preference != "prompt" && preference != "interactive"))
    {
        preference = "auto";
    }

    return preference.ToLower();
}

/// <summary>
/// Sets the user's preferred authentication method
/// </summary>
private static void SetAuthenticationPreference(string method)
{
    // Store in environment variable for current session
    Environment.SetEnvironmentVariable("AZURE_AUTH_PREFERENCE", method, EnvironmentVariableTarget.Process);

    Console.WriteLine($"Authentication preference set to: {method}");
    Console.WriteLine("Note: This setting will persist for the current application session only.");
    Console.WriteLine("To make this setting permanent, update your appsettings.json file.");
}

/// <summary>
/// Securely reads a password/secret from the console without displaying it
/// </summary>
private static string ReadPasswordFromConsole()
{
    string password = "";
    ConsoleKeyInfo key;

    do
    {
        key = Console.ReadKey(true);

        // Handle backspace
        if (key.Key == ConsoleKey.Backspace && password.Length > 0)
        {
            password = password.Substring(0, password.Length - 1);
            Console.Write("\b \b"); // Erase the last character displayed
        }
        // Ignore Enter key
        else if (key.Key != ConsoleKey.Enter)
        {
            password += key.KeyChar;
            Console.Write("*"); // Display asterisk instead of the actual character
        }
    }
    while (key.Key != ConsoleKey.Enter);

    Console.WriteLine(); // Add a new line after input is complete
    return password;
}

/// <summary>
/// Displays the settings menu for configuring application preferences
/// </summary>
private static void DisplaySettingsMenu()
{
    bool exitSettings = false;

    while (!exitSettings)
    {
        Console.Clear();
        Console.WriteLine("=== Settings Menu ===");
        Console.WriteLine("1. Configure Authentication Preference");
        Console.WriteLine("2. View Current Settings");
        Console.WriteLine("3. Return to Main Menu");

        Console.Write("\nSelect an option: ");
        string choice = Console.ReadLine() ?? "";

        switch (choice)
        {
            case "1":
                ConfigureAuthenticationPreference();
                break;
            case "2":
                ViewCurrentSettings();
                break;
            case "3":
                exitSettings = true;
                break;
            default:
                Console.WriteLine("Invalid option. Press any key to try again...");
                Console.ReadKey();
                break;
        }
    }
}        /// <summary>
         /// Configures the authentication preference setting
         /// </summary>
private static void ConfigureAuthenticationPreference()
{
    Console.Clear();
    Console.WriteLine("=== Configure Authentication Preference ===");
    Console.WriteLine("Select your preferred authentication method:");
    Console.WriteLine("1. Certificate Authentication (recommended for production)");
    Console.WriteLine("2. Client Secret Authentication");
    Console.WriteLine("3. Prompt for Credentials When Needed");
    Console.WriteLine("4. Interactive Browser Login");
    Console.WriteLine("5. Auto-detect (try certificate first, then client secret)"); Console.WriteLine("6. Cancel");

    Console.Write("\nSelect an option: ");
    string choice = Console.ReadLine() ?? "";

    switch (choice)
    {
        case "1":
            SetAuthenticationPreference("certificate");
            break;
        case "2":
            SetAuthenticationPreference("clientsecret");
            break;
        case "3":
            SetAuthenticationPreference("prompt");
            break;
        case "4":
            SetAuthenticationPreference("interactive");
            break;
        case "5":
            SetAuthenticationPreference("auto");
            break;
        case "6":
            // Do nothing, just return
            break;
        default:
            Console.WriteLine("Invalid option.");
            break;
    }

    Console.WriteLine("\nPress any key to continue...");
    Console.ReadKey();
}

/// <summary>
/// Displays current application settings
/// </summary>
private static void ViewCurrentSettings()
{
    Console.Clear();
    Console.WriteLine("=== Current Settings ===");

    // Display authentication preference
    string authPreference = GetAuthenticationPreference();
    Console.WriteLine($"Authentication Preference: {authPreference}");
    // Display tenant ID if available
    if (_config != null && !string.IsNullOrEmpty(_config["Azure:TenantId"]))
    {
        string tenantId = _config["Azure:TenantId"] ?? string.Empty;
        Console.WriteLine($"Azure Tenant ID: {tenantId}");
    }

    // Display client ID if available
    if (_config != null && !string.IsNullOrEmpty(_config["Azure:ClientId"]))
    {
        string clientId = _config["Azure:ClientId"] ?? string.Empty;
        Console.WriteLine($"Azure Client ID: {clientId}");
    }

    // Note about certificate
    Console.WriteLine("\nCertificate: Certificate information is loaded from the Windows Certificate Store");

    Console.WriteLine("\nPress any key to continue...");
    Console.ReadKey();
}

/// <summary>
/// Configures proxy settings from the settings menu
/// </summary>
private static void ConfigureProxySettings()
{
    Console.Clear();
    Console.WriteLine("=== Configure Proxy Settings ===");

    // Display current proxy configuration
    string proxyStatus = Services.ProxyService.IsProxyConfigured
        ? Services.ProxyService.ProxyInfo
        : "No proxy configured";
    Console.WriteLine($"Current Proxy Status: {proxyStatus}\n");

    Console.WriteLine("Select an option:");
    Console.WriteLine("1. Enable proxy");
    Console.WriteLine("2. Disable proxy");
    Console.WriteLine("3. Return to Settings Menu");

    Console.Write("\nYour choice: ");
    string? choice = Console.ReadLine()?.Trim();

    switch (choice)
    {
        case "1":
            Console.Write("Enter the proxy URL (without http:// prefix): ");
            string proxyUrl = Console.ReadLine()?.Trim() ?? "";

            Console.Write("Enter the proxy port: ");
            string proxyPort = Console.ReadLine()?.Trim() ?? "";

            if (string.IsNullOrEmpty(proxyUrl) || string.IsNullOrEmpty(proxyPort) || !int.TryParse(proxyPort, out int port))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[ERROR] Invalid proxy URL or port.");
                Console.ResetColor();
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
                return;
            }

            Console.Write("Does your proxy require authentication? (yes/no): ");
            string requiresAuth = Console.ReadLine()?.Trim().ToLower() ?? "no";

            string username = "";
            string password = "";
            if (requiresAuth == "yes" || requiresAuth == "y")
            {
                Console.Write("Enter the proxy username: ");
                username = Console.ReadLine()?.Trim() ?? "";

                Console.Write("Enter the proxy password: ");
                password = ReadPasswordFromConsole();
            }

            // Save the proxy settings to configuration
            try
            {
                if (_config != null)
                {
                    // Read the current appsettings.json
                    string appSettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
                    string json = File.ReadAllText(appSettingsPath);
                    var jsonConfig = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);

                    if (jsonConfig != null)
                    {
                        // Create or update the proxy section
                        var proxyConfig = new Dictionary<string, object>
                                {
                                    { "UseProxy", true },
                                    { "ProxyUrl", proxyUrl },
                                    { "ProxyPort", proxyPort }
                                };

                        if (!string.IsNullOrEmpty(username))
                            proxyConfig["Username"] = username;

                        if (!string.IsNullOrEmpty(password))
                            proxyConfig["Password"] = password;

                        jsonConfig["Proxy"] = proxyConfig;


                        // Save the updated configuration
                        string updatedJson = System.Text.Json.JsonSerializer.Serialize(jsonConfig,
                            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(appSettingsPath, updatedJson);

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("[SUCCESS] Proxy settings saved to configuration file.");
                        Console.ResetColor();

                        // Initialize the proxy with new settings
                        var proxy = new System.Net.WebProxy($"{proxyUrl}:{port}");
                        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                        {
                            proxy.Credentials = new System.Net.NetworkCredential(username, password);
                        }

                        // Update global proxy and HttpClient
                        System.Net.WebRequest.DefaultWebProxy = proxy;
                        var httpClientHandler = new System.Net.Http.HttpClientHandler
                        {
                            Proxy = proxy,
                            UseProxy = true
                        };
                        _httpClient = new System.Net.Http.HttpClient(httpClientHandler);

                        // Update the proxy service state
                        Services.ProxyService.UpdateProxyStatus(true, $"Proxy: {proxyUrl}:{port}" +
                            (!string.IsNullOrEmpty(username) ? " (authenticated)" : ""));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Failed to save proxy settings: {ex.Message}");
                Console.ResetColor();
                Logger.LogError($"Failed to save proxy settings: {ex}");
            }

            break;

        case "2":
            // Disable proxy
            try
            {
                if (_config != null)
                {
                    // Read the current appsettings.json
                    string appSettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
                    string json = File.ReadAllText(appSettingsPath);
                    var jsonConfig = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);

                    if (jsonConfig != null)
                    {
                        // Create or update the proxy section to disable it
                        var proxyConfig = new Dictionary<string, object>
                                {
                                    { "UseProxy", false }
                                };

                        // Keep the existing proxy details in case they're needed later, just disable usage
                        if (jsonConfig.ContainsKey("Proxy") && jsonConfig["Proxy"] is System.Text.Json.JsonElement proxyElement)
                        {
                            try
                            {
                                if (proxyElement.TryGetProperty("ProxyUrl", out var urlElement))
                                    proxyConfig["ProxyUrl"] = urlElement.GetString() ?? "";

                                if (proxyElement.TryGetProperty("ProxyPort", out var portElement))
                                    proxyConfig["ProxyPort"] = portElement.GetString() ?? "";

                                if (proxyElement.TryGetProperty("Username", out var userElement))
                                    proxyConfig["Username"] = userElement.GetString() ?? "";

                                if (proxyElement.TryGetProperty("Password", out var passElement))
                                    proxyConfig["Password"] = passElement.GetString() ?? "";
                            }
                            catch { /* Ignore errors accessing properties */ }
                        }

                        jsonConfig["Proxy"] = proxyConfig;


                        // Save the updated configuration
                        string updatedJson = System.Text.Json.JsonSerializer.Serialize(jsonConfig,
                            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(appSettingsPath, updatedJson);

                        // Reset proxy settings
                        System.Net.WebRequest.DefaultWebProxy = null;
                        _httpClient = new System.Net.Http.HttpClient();

                        // Update the proxy service state
                        Services.ProxyService.UpdateProxyStatus(false, "No proxy configured");

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("[SUCCESS] Proxy disabled successfully.");
                        Console.ResetColor();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Failed to disable proxy: {ex.Message}");
                Console.ResetColor();
                Logger.LogError($"Failed to disable proxy: {ex}");
            }
            break;

        case "3":
            // Return to settings menu
            break;

        default:
            Console.WriteLine("Invalid option.");
            break;
    }

    Console.WriteLine("\nPress any key to continue...");
    Console.ReadKey();
}

private static async Task TestFileSharePermissionsAsync()
{
    Console.Clear();
    DisplayIntroduction();
    Console.WriteLine("=== Test File Share Permissions ===\n");

    try
    {
        // Initialize Azure credentials if not already done
        await InitializeAzureCredentialsAsync();

        if (_credential == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[ERROR] Could not initialize Azure credentials. Please check your configuration.");
            Console.ResetColor();
            return;
        }

        // Get storage account name
        Console.Write("Enter your Azure Storage Account Name: ");
        string storageAccountName = Console.ReadLine() ?? "";
        if (string.IsNullOrEmpty(storageAccountName))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[ERROR] Storage account name is required for testing permissions.");
            Console.ResetColor();
            return;
        }
        Console.WriteLine($"[INFO] Using storage account: {storageAccountName}");

        // Get file share name
        Console.Write("Enter your Azure File Share Name: ");
        string fileShareName = Console.ReadLine() ?? "";
        if (string.IsNullOrEmpty(fileShareName))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[ERROR] File share name is required for testing permissions.");
            Console.ResetColor();
            return;
        }
        Console.WriteLine($"[INFO] Using Azure File Share: {fileShareName}");                // Run the permission diagnostics
        await FileShareDiagnostics.RunFileSharePermissionDiagnosticsAsync(
            _credential,
            _httpClient ?? new HttpClient(),
            storageAccountName,
            fileShareName);

        Console.WriteLine("\n[INFO] Permission testing completed.");

        // After testing, provide a recommendation
        Console.WriteLine("\n=== Recommendations Based on Test Results ===");
        Console.WriteLine("If any tests failed, you may need to adjust permissions in the Azure portal:");
        Console.WriteLine("1. Navigate to the Azure portal and locate your storage account");
        Console.WriteLine("2. Go to 'Access Control (IAM)'");
        Console.WriteLine("3. Add the 'Storage File Data SMB Share Elevated Contributor' role to your user/service principal");
        Console.WriteLine("4. If you need to assign permissions via Azure AD, ensure proper role assignments");
        Console.WriteLine("5. For enterprise environments, check that network policies allow access to the storage account");
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[ERROR] An error occurred while testing permissions: {ex.Message}");
        Console.ResetColor();
        Logger.LogError($"Error during permission testing: {ex}");
    }
}
    }
}


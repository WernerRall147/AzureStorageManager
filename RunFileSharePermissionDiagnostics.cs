using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Storage.Files.Shares;
using AzureStorageManager.Services;
using System;
using System.Threading.Tasks;

namespace AzureStorageManager
{
    public static class FileShareDiagnostics
    {
        /// <summary>
        /// Runs a detailed permission diagnostic test on Azure File Share to identify permission issues
        /// </summary>
        public static async Task RunFileSharePermissionDiagnosticsAsync(
            TokenCredential credential, 
            HttpClient httpClient, 
            string storageAccountName,
            string fileShareName)
        {
            Console.Clear();
            Console.WriteLine("=== Azure File Share Permission Diagnostics ===\n");
            Console.WriteLine("This tool will test various operations on your Azure File Share to identify");
            Console.WriteLine("which specific permissions you have and which ones are missing.");
            Console.WriteLine("It helps diagnose 'Access Denied' errors when working with File Shares.\n");
            
            if (string.IsNullOrEmpty(storageAccountName))
            {
                Console.Write("Enter your Azure Storage Account Name: ");
                storageAccountName = Console.ReadLine() ?? "";
                if (string.IsNullOrEmpty(storageAccountName))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("[ERROR] Storage account name is required to run diagnostics.");
                    Console.ResetColor();
                    return;
                }
            }
            else
            {
                Console.WriteLine($"[INFO] Using storage account: {storageAccountName}");
            }
            
            // Get file share name if not provided
            if (string.IsNullOrEmpty(fileShareName))
            {
                Console.Write("Enter your Azure File Share Name: ");
                fileShareName = Console.ReadLine() ?? "";
                if (string.IsNullOrEmpty(fileShareName))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("[ERROR] File share name is required to run diagnostics.");
                    Console.ResetColor();
                    return;
                }
            }
            else
            {
                Console.WriteLine($"[INFO] Using file share: {fileShareName}");
            }
            
            try
            {
                // Create ShareClientOptions with proxy if configured
                var shareClientOptions = new ShareClientOptions();
                if (httpClient != null)
                {
                    Console.WriteLine("[INFO] Using custom HTTP client with proxy configuration");
                    shareClientOptions.Transport = new HttpClientTransport(httpClient);
                }
                
                // Create the FileShareService
                Console.WriteLine($"[INFO] Creating connection to {storageAccountName}.file.core.windows.net");
                var fileShareService = new FileShareService(
                    $"https://{storageAccountName}.file.core.windows.net", 
                    fileShareName, 
                    credential, 
                    shareClientOptions);
                
                // Run the diagnostics
                Console.WriteLine("[INFO] Starting permission diagnostics...");
                await fileShareService.DiagnosePermissionsAsync();
                
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("\n===================================================");
                Console.WriteLine("Permission Diagnostics Complete");
                Console.WriteLine("===================================================");
                Console.ResetColor();
                
                Console.WriteLine("\nBased on the permission test results, you may need to adjust your Azure IAM role assignments.");
                Console.WriteLine("Remember that for copying files to Azure File Shares, you typically need one of these roles:");
                Console.WriteLine(" - Storage File Data SMB Share Elevated Contributor (recommended for most scenarios)");
                Console.WriteLine(" - Storage File Data Privileged Contributor (for scenarios requiring permission override)");
                
                Console.WriteLine("\nTo update your permissions:");
                Console.WriteLine("1. Go to the Azure Portal");
                Console.WriteLine("2. Navigate to your Storage Account");
                Console.WriteLine("3. Select 'Access Control (IAM)'");
                Console.WriteLine("4. Click '+ Add' and select 'Add role assignment'");
                Console.WriteLine("5. Choose the appropriate role from the above options");
                Console.WriteLine("6. Assign it to your user account or service principal");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] An error occurred during the diagnostics: {ex.Message}");
                Console.ResetColor();
                Utilities.Logger.LogError($"File share permission diagnostics failed: {ex}");
            }
        }
    }
}

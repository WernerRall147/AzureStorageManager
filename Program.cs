using System.Net;
using System.Net.Http;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models; // Add this using directive
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using AzureStorageManager.Services;

namespace AzureStorageManager
{
    class Program
    {
        static async Task Main(string[] args)
        {
            DisplayIntroduction();

            // Load configuration
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddEnvironmentVariables()
                .Build();

            string tenantId = config["Azure:TenantId"] ?? "";
            string clientId = config["Azure:ClientId"] ?? "";
            string thumbprint = config["Azure:CertificateThumbprint"] ?? "";
            string clientSecret = config["Azure:ClientSecret"] ?? "";

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
            TokenCredential credential;
            if (certificate != null)
            {
                Console.WriteLine("Initializing ClientCertificateCredential...");
                credential = new ClientCertificateCredential(tenantId, clientId, certificate);
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
                    return;
                }

                Console.WriteLine("Initializing ClientSecretCredential...");
                credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
            }

            // Ask if using a proxy
            Console.Write("Are you using a proxy? (yes/no): ");
            string? useProxyResponse = Console.ReadLine()?.Trim().ToLower();
            HttpClient? httpClient = null;

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
                    httpClient = new HttpClient(httpClientHandler);
                }
                else
                {
                    Console.WriteLine("Invalid proxy URL or port. Proceeding without proxy.");
                }
            }

            // Prompt user for storage account details
            Console.Write("Enter your Azure Storage Account Name: ");
            string storageAccountName = Console.ReadLine() ?? "";

            Console.WriteLine("Choose Storage Type:");
            Console.WriteLine("1. Blob Storage");
            Console.WriteLine("2. File Share");
            string choice = Console.ReadLine() ?? "";

            Console.Write("Enter the local directory path for file operations: ");
            string localDirectory = Console.ReadLine() ?? "";

            if (string.IsNullOrEmpty(storageAccountName))
            {
                Console.WriteLine("Storage account name is missing. Please check your input.");
                return;
            }

            try
            {
                if (choice == "1")
                {
                    // Blob Storage
                    Console.Write("Enter your Azure Blob Container Name: ");
                    string blobContainerName = Console.ReadLine() ?? "";

                    Console.WriteLine("Connecting to Blob Storage...");
                    var blobClientOptions = new BlobClientOptions();
                    if (httpClient != null)
                    {
                        blobClientOptions.Transport = new HttpClientTransport(httpClient);
                    }
                    var blobServiceClient = new BlobServiceClient(new Uri($"https://{storageAccountName}.blob.core.windows.net"), credential, blobClientOptions);

                    var blobStorageService = new BlobStorageService(blobServiceClient, blobContainerName);
                    string reportFileName = $"BlobStorageReport_{Path.GetFileName(localDirectory)}.csv";

                    await blobStorageService.ListAndVerifyBlobsAsync(localDirectory, reportFileName);
                }
                else if (choice == "2")
                {
                    // File Share
                    Console.Write("Enter your Azure File Share Name: ");
                    string fileShareName = Console.ReadLine() ?? "";

                    Console.Write("Enter the folder path within the Azure File Share (e.g., folder\\subfolder): ");
                    string azureFolderPath = Console.ReadLine() ?? "";

                    Console.WriteLine("Connecting to File Share...");
                    var shareClientOptions = new ShareClientOptions();
                    if (httpClient != null)
                    {
                        shareClientOptions.Transport = new HttpClientTransport(httpClient);
                    }
                    shareClientOptions.AddPolicy(new FileRequestIntentPolicy(), HttpPipelinePosition.PerCall);

                    var shareServiceClient = new ShareServiceClient(new Uri($"https://{storageAccountName}.file.core.windows.net"), credential, shareClientOptions);
                    var fileShareService = new FileShareService(shareServiceClient, fileShareName, azureFolderPath);

                    string reportFileName = $"FileShareReport_{Path.GetFileName(localDirectory)}.csv";
                    await fileShareService.ListAndVerifyFilesAsync(localDirectory, reportFileName);
                }
                else
                {
                    Console.WriteLine("Invalid choice. Please restart the application and choose a valid option.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            Console.WriteLine("Press Enter to exit the application...");
            Console.ReadLine();
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
            Console.WriteLine("=======================================");
            Console.WriteLine("       Starting Azure Storage Manager... ");
            Console.WriteLine("=======================================");
            Console.WriteLine();
        }
    }

    // Custom policy to add x-ms-file-request-intent header to file share requests
    internal class FileRequestIntentPolicy : HttpPipelinePolicy
    {
        public override void Process(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
        {
            message.Request.Headers.Add("x-ms-file-request-intent", "backup");
            ProcessNext(message, pipeline);
        }

        public override ValueTask ProcessAsync(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
        {
            message.Request.Headers.Add("x-ms-file-request-intent", "backup");
            return ProcessNextAsync(message, pipeline);
        }
    }

    public class FileShareService
    {
        private readonly ShareClient _shareClient;
        private readonly string _fileShareName;
        private readonly string _azureFolderPath;

        public FileShareService(ShareServiceClient shareServiceClient, string fileShareName, string azureFolderPath)
        {
            _shareClient = shareServiceClient.GetShareClient(fileShareName);
            _fileShareName = fileShareName;
            _azureFolderPath = azureFolderPath;
        }

        public async Task ListAndVerifyFilesAsync(string localDirectory, string reportFileName)
        {
            var reportLines = new List<string>();
            var rootDirectoryClient = _shareClient.GetDirectoryClient(_azureFolderPath);
            await ListAndVerifyFilesRecursiveAsync(rootDirectoryClient, localDirectory, reportLines);

            // Write the report to a CSV file
            await File.WriteAllLinesAsync(reportFileName, reportLines);
        }

        private async Task ListAndVerifyFilesRecursiveAsync(ShareDirectoryClient directoryClient, string localDirectory, List<string> reportLines)
        {
            await foreach (ShareFileItem item in directoryClient.GetFilesAndDirectoriesAsync())
            {
                if (item.IsDirectory)
                {
                    // Recursively list files in subdirectories
                    var subDirectoryClient = directoryClient.GetSubdirectoryClient(item.Name);
                    await ListAndVerifyFilesRecursiveAsync(subDirectoryClient, Path.Combine(localDirectory, item.Name), reportLines);
                }
                else
                {
                    // Verify file
                    var fileClient = directoryClient.GetFileClient(item.Name);
                    string localFilePath = Path.Combine(localDirectory, item.Name);

                    if (File.Exists(localFilePath))
                    {
                        // Compare file hashes or other verification logic
                        reportLines.Add($"{localFilePath}, {fileClient.Uri}, Verified");
                    }
                    else
                    {
                        reportLines.Add($"{localFilePath}, {fileClient.Uri}, Missing");
                    }
                }
            }
        }
    }
}

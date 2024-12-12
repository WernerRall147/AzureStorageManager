using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Files.Shares;
using AzureStorageManager.Services;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;

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
                // AddEnvironmentVariables requires Microsoft.Extensions.Configuration.EnvironmentVariables package
                .AddEnvironmentVariables()
                .Build();

            string tenantId = config["Azure:TenantId"] ?? "";
            string clientId = config["Azure:ClientId"] ?? "";
            string thumbprint = config["Azure:CertificateThumbprint"] ?? "";
            string clientSecret = config["Azure:ClientSecret"] ?? "";

            // Load certificate from Windows Certificate Store
            Console.WriteLine("Loading certificate from Windows Certificate Store...");
            X509Certificate2 certificate = null;
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
                    var blobServiceClient = new BlobServiceClient(new Uri($"https://{storageAccountName}.blob.core.windows.net"), credential);

                    var blobStorageService = new BlobStorageService(blobServiceClient, blobContainerName);
                    string reportFileName = $"BlobStorageReport_{Path.GetFileName(localDirectory)}.csv";

                    await blobStorageService.ListAndVerifyBlobsAsync(localDirectory, reportFileName);
                }
                else if (choice == "2")
                {
                    // File Share
                    Console.Write("Enter your Azure File Share Name: ");
                    string fileShareName = Console.ReadLine() ?? "";

                    Console.WriteLine("Connecting to File Share...");
                    var shareClientOptions = new ShareClientOptions();
                    shareClientOptions.AddPolicy(new FileRequestIntentPolicy(), HttpPipelinePosition.PerCall);

                    var fileShareService = new FileShareService(
                        $"https://{storageAccountName}.file.core.windows.net",
                        fileShareName,
                        credential,
                        shareClientOptions);

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
}

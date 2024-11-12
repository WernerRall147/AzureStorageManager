using System;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Files.Shares;
using AzureStorageManager.Services;

namespace AzureStorageManager
{
    class Program
    {
        static async Task Main(string[] args)
        {
            DisplayIntroduction();

            // Prompt user for storage account details
            Console.Write("Enter your Azure Storage Account Name: ");
            string storageAccountName = Console.ReadLine() ?? "";

            Console.WriteLine("Choose Storage Type:");
            Console.WriteLine("1. Blob Storage");
            Console.WriteLine("2. File Share");

            string choice = Console.ReadLine() ?? ""; // Ensures no null value

            // Prompt user for local directory path
            Console.Write("Enter the local directory path for file operations: ");
            string localDirectory = Console.ReadLine() ?? "";

            // Check if storage account name is present
            if (string.IsNullOrEmpty(storageAccountName))
            {
                Console.WriteLine("Storage account name is missing. Please check your input.");
                return;
            }

            if (choice == "1")
            {
                Console.Write("Enter your Azure Blob Container Name: ");
                string blobContainerName = Console.ReadLine() ?? "";

                Console.WriteLine("Connecting to Blob Storage...");

                try
                {
                    // Initialize BlobServiceClient with Managed Identity
                    var blobServiceClient = new BlobServiceClient(
                        new Uri($"https://{storageAccountName}.blob.core.windows.net"),
                        new DefaultAzureCredential());

                    var blobStorageService = new BlobStorageService(blobServiceClient, blobContainerName);
                    await blobStorageService.ListAndVerifyBlobsAsync(localDirectory);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error connecting to Blob Storage: {ex.Message}");
                }
            }
            else if (choice == "2")
            {
                Console.Write("Enter your Azure File Share Name: ");
                string fileShareName = Console.ReadLine() ?? "";

                Console.WriteLine("Connecting to File Share...");

                try
                {
                    if (string.IsNullOrEmpty(fileShareName))
                    {
                        Console.WriteLine("File share name is missing. Please check your input.");
                        return;
                    }

                    // Initialize ShareServiceClient with Managed Identity
                    string fileShareUri = $"https://{storageAccountName}.file.core.windows.net";
                    var shareServiceClient = new ShareServiceClient(
                        new Uri(fileShareUri),
                        new DefaultAzureCredential());

                    var shareClient = shareServiceClient.GetShareClient(fileShareName);

                    var fileShareService = new FileShareService(shareClient);
                    await fileShareService.ListAndVerifyFilesAsync(localDirectory);
                }
                catch (UriFormatException uriEx)
                {
                    Console.WriteLine($"Invalid URI format: {uriEx.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error connecting to File Share: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("Invalid choice. Please restart the application and choose a valid option.");
            }
        }

        private static void DisplayIntroduction()
        {
            Console.WriteLine("=======================================");
            Console.WriteLine("        Azure Storage Manager Tool      ");
            Console.WriteLine("=======================================");
            Console.WriteLine();
            Console.WriteLine("Welcome to the Azure Storage Manager Tool!");
            Console.WriteLine("This tool helps you verify file integrity by comparing local files with those stored in Azure.");
            Console.WriteLine("It checks that files in Azure Blob Storage or File Shares match your local copies, ensuring data accuracy.");
            Console.WriteLine();
            Console.WriteLine("When should you run this tool?");
            Console.WriteLine("- After uploading files to Azure Storage, to confirm successful uploads.");
            Console.WriteLine("- Periodically, to ensure ongoing data integrity and detect any unexpected changes.");
            Console.WriteLine("- After restoring files from Azure, as part of a disaster recovery or backup validation process.");
            Console.WriteLine("- When migrating files within or between storage accounts, to confirm files were migrated accurately.");
            Console.WriteLine();
            Console.WriteLine("Requirements:");
            Console.WriteLine("- Azure Storage Account Name");
            Console.WriteLine("- Name of the Blob Container or File Share to be verified");
            Console.WriteLine("- Local directory path containing the original files for comparison");
            Console.WriteLine();
            Console.WriteLine("How to Use:");
            Console.WriteLine("1. Enter your Azure Storage Account name when prompted.");
            Console.WriteLine("2. Choose the storage type (Blob or File Share) and specify the container/share name.");
            Console.WriteLine("3. Enter the local directory path where your files are stored.");
            Console.WriteLine("The tool will compare each file's MD5 hash with the stored hash in Azure, and");
            Console.WriteLine("generate a report showing any mismatches.");
            Console.WriteLine();
            Console.WriteLine("=======================================");
            Console.WriteLine("       Starting Azure Storage Manager... ");
            Console.WriteLine("=======================================");
            Console.WriteLine();
        }
    }
}

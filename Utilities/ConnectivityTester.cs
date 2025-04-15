using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Storage;
using Microsoft.Extensions.Configuration;

namespace AzureStorageManager.Utilities
{
    /// <summary>
    /// Utility to test Azure Storage connectivity and list accessible storage accounts
    /// </summary>
    public class ConnectivityTester
    {
        private readonly IConfiguration _configuration;
        
        public ConnectivityTester(string configPath = "appsettings.json")
        {
            // Load configuration
            _configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(configPath, optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();
        }

        public async Task<List<string>> TestConnectivityAsync()
        {
            Console.WriteLine("Testing Azure Storage connectivity...");
            var results = new List<string>();
            
            try
            {
                // Get credentials based on configured auth method
                var credential = GetCredential();
                
                // Create a client
                var armClient = new ArmClient(credential);
                
                // Get subscription list
                Console.WriteLine("Retrieving subscriptions...");
                var subscriptions = armClient.GetSubscriptions();
                
                // For each subscription, get all storage accounts
                foreach (var subscription in subscriptions)
                {
                    Console.WriteLine($"Checking subscription: {subscription.Data.DisplayName}");
                    var storageAccounts = subscription.GetStorageAccounts();
                    
                    // List all storage accounts in the subscription
                    foreach (var account in storageAccounts)
                    {
                        string result = $"✓ {account.Data.Name} (Location: {account.Data.Location}, Kind: {account.Data.Kind})";
                        Console.WriteLine(result);
                        results.Add(result);
                    }
                }
                
                if (results.Count == 0)
                {
                    Console.WriteLine("No storage accounts found or no permissions to access them.");
                    results.Add("No storage accounts found or no permissions to access them.");
                }
            }
            catch (Exception ex)
            {
                string errorMessage = $"❌ Error connecting to Azure: {ex.Message}";
                Console.WriteLine(errorMessage);
                
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                
                results.Add(errorMessage);
            }
            
            return results;
        }
        
        private TokenCredential GetCredential()
        {
            var azureConfig = _configuration.GetSection("Azure");
            string tenantId = azureConfig["TenantId"];
            string clientId = azureConfig["ClientId"];
            string certificateThumbprint = azureConfig["CertificateThumbprint"];
            string clientSecret = azureConfig["ClientSecret"];
            
            var authPreference = _configuration.GetSection("Authentication")["PreferredMethod"]?.ToLower() ?? "";
            
            Console.WriteLine($"Auth method: {(string.IsNullOrEmpty(authPreference) ? "Not specified, trying available methods" : authPreference)}");
            
            // Try certificate authentication
            if (authPreference == "certificate" || string.IsNullOrEmpty(authPreference))
            {
                if (!string.IsNullOrEmpty(certificateThumbprint) && !certificateThumbprint.Contains("your-"))
                {
                    Console.WriteLine("Using certificate authentication");
                    return new ClientCertificateCredential(tenantId, clientId, certificateThumbprint);
                }
            }
            
            // Try client secret authentication
            if (authPreference == "clientsecret" || string.IsNullOrEmpty(authPreference))
            {
                if (!string.IsNullOrEmpty(clientSecret) && !clientSecret.Contains("your-"))
                {
                    Console.WriteLine("Using client secret authentication");
                    return new ClientSecretCredential(tenantId, clientId, clientSecret);
                }
            }
            
            // Default to interactive authentication
            Console.WriteLine("Using interactive browser authentication");
            return new InteractiveBrowserCredential(new InteractiveBrowserCredentialOptions 
            { 
                TenantId = tenantId,
                ClientId = clientId,
                RedirectUri = new Uri("http://localhost")
            });
        }

        public static async Task Main(string[] args)
        {
            string configPath = "appsettings.json";
            if (args.Length > 0)
            {
                configPath = args[0];
            }
            
            var tester = new ConnectivityTester(configPath);
            await tester.TestConnectivityAsync();
            
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}

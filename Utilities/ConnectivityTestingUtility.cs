using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using AzureStorageManager.Utilities;

namespace AzureStorageManager
{
    /// <summary>
    /// Utility class for testing Azure connectivity from the settings menu
    /// </summary>
    public static class ConnectivityTestingUtility
    {
        /// <summary>
        /// Tests connectivity to Azure Storage and lists accessible storage accounts
        /// </summary>
        public static async Task TestAzureConnectivity()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("===================================================");
            Console.WriteLine("             Azure Connectivity Test               ");
            Console.WriteLine("===================================================");
            Console.ResetColor();

            Console.WriteLine("\nTesting connection to Azure Storage based on your settings...");
            Console.WriteLine("This will list all storage accounts you have access to.\n");

            try
            {
                // Create an instance of the connectivity tester
                var connectivityTester = new ConnectivityTester();
                
                // Show a progress indicator
                Console.WriteLine("Connecting to Azure...");
                
                // Run the connectivity test
                var results = await connectivityTester.TestConnectivityAsync();
                Console.WriteLine("\n");

                // Display results with color coding
                Console.WriteLine("Results:");
                Console.WriteLine("---------------------------------------------------");
                
                foreach (var result in results)
                {
                    if (result.StartsWith("✓"))
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                    }
                    else if (result.StartsWith("❌"))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                    }
                    
                    Console.WriteLine(result);
                    Console.ResetColor();
                }
                
                Console.WriteLine("---------------------------------------------------");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error during connectivity test: {ex.Message}");
                
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                
                Console.ResetColor();
            }

            Console.WriteLine("\nPress any key to return to settings...");
            Console.ReadKey();
        }
    }
}

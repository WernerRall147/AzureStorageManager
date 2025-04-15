using System;
using System.Threading.Tasks;

namespace AzureStorageManager.Utilities
{
    /// <summary>
    /// A simple wrapper class to run the ConnectivityTester synchronously from the main program
    /// </summary>
    public static class ConnectivityTestRunner
    {
        /// <summary>
        /// Runs the connectivity test and blocks until completion
        /// </summary>
        public static void RunConnectivityTest()
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
                Console.WriteLine("This may take a moment...\n");
                
                // Run the connectivity test synchronously
                var results = connectivityTester.TestConnectivityAsync().GetAwaiter().GetResult();

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

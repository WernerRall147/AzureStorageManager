using System;
using System.Threading;
using System.Threading.Tasks;

namespace AzureStorageManager.Utilities
{    /// <summary>
    /// Helper class for showing progress indicators during long-running operations
    /// </summary>
    public static class ProgressIndicator
    {
        // Using ASCII characters for better compatibility with all terminal environments
        private static readonly string[] Spinner = new[] { "|", "/", "-", "\\" };
        
        /// <summary>
        /// Gets the compatible spinner characters array - use this throughout the application
        /// </summary>
        public static string[] GetSpinnerChars()
        {
            return Spinner;
        }
        
        /// <summary>
        /// Starts a simple spinner progress indicator
        /// </summary>
        /// <param name="message">The message to display alongside the spinner</param>
        /// <param name="cancellationToken">Token to cancel the progress indicator</param>
        /// <returns>A Task representing the spinner animation</returns>
        public static Task StartSpinner(string message, CancellationToken cancellationToken)
        {
            return Task.Run(async () => 
            {
                int spinnerPos = 0;
                var startTime = DateTime.Now;
                
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var elapsed = DateTime.Now - startTime;
                        Console.Write($"\r[WORKING] {Spinner[spinnerPos]} {message} ({elapsed.Minutes:00}:{elapsed.Seconds:00})      ");
                        
                        spinnerPos = (spinnerPos + 1) % Spinner.Length;
                        await Task.Delay(200, cancellationToken);
                    }
                }
                catch (TaskCanceledException)
                {
                    // Expected when cancellation is requested
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                }
                catch (Exception)
                {
                    // Suppress any other exceptions in the progress indicator
                }
            }, cancellationToken);
        }
        
        /// <summary>
        /// Starts a progress indicator for file operations showing count and percentage
        /// </summary>
        /// <param name="getStatusFunc">Function that returns the current status string</param>
        /// <param name="cancellationToken">Token to cancel the progress indicator</param>
        /// <returns>A Task representing the progress animation</returns>
        public static Task StartProgress(Func<string> getStatusFunc, CancellationToken cancellationToken)
        {
            return Task.Run(async () => 
            {
                int spinnerPos = 0;
                
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        string status = getStatusFunc();
                        Console.Write($"\r[WORKING] {Spinner[spinnerPos]} {status}      ");
                        
                        spinnerPos = (spinnerPos + 1) % Spinner.Length;
                        await Task.Delay(200, cancellationToken);
                    }
                }
                catch (TaskCanceledException)
                {
                    // Expected when cancellation is requested
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                }
                catch (Exception)
                {
                    // Suppress any other exceptions in the progress indicator
                }
            }, cancellationToken);
        }
        
        /// <summary>
        /// Stops a progress indicator and writes the final status message
        /// </summary>
        /// <param name="cts">The CancellationTokenSource to cancel</param>
        /// <param name="progressTask">The Task running the progress indicator</param>
        /// <param name="successMessage">The success message to display</param>
        public static void StopProgress(CancellationTokenSource cts, Task progressTask, string successMessage)
        {
            try
            {
                // Cancel and wait for the task to complete
                cts.Cancel();
                try
                {
                    progressTask.Wait(1000); // Give it 1 second to terminate
                }
                catch { } // Ignore any exceptions from the progress task
                
                // Clear the line and write the final message
                Console.Write("\r" + new string(' ', 100));  // Clear the line
                Console.WriteLine($"\r{successMessage}");
            }
            finally
            {
                cts.Dispose();
            }
        }
    }
}

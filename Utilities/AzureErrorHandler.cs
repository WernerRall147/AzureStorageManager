using Azure;
using System;
using System.Text;

namespace AzureStorageManager.Utilities
{
    public static class AzureErrorHandler
    {
        /// <summary>
        /// Handles Azure Storage exceptions with user-friendly messages
        /// </summary>
        /// <param name="ex">The RequestFailedException from Azure</param>
        /// <param name="operationName">Name of the operation being performed</param>
        /// <param name="resourceId">Optional resource identifier (container name, file share, etc.)</param>
        public static void HandleStorageException(RequestFailedException ex, string operationName, string resourceId = "")
        {
            Console.ForegroundColor = ConsoleColor.Red;
            
            if (ex.Status == 403)
            {
                Console.WriteLine($"[ERROR] Authorization Error: You don't have permission to {operationName} {resourceId}.");
                Console.WriteLine("[ERROR] Please ensure your account has the appropriate RBAC role assignments.");
                Console.WriteLine("        For blob operations: 'Storage Blob Data Contributor'");
                Console.WriteLine("        For file share operations: 'Storage File Data SMB Share Elevated Contributor'");
            }
            else if (ex.Status == 404)
            {
                Console.WriteLine($"[ERROR] Not Found: The resource {resourceId} does not exist.");
            }
            else if (ex.Status == 409)
            {
                Console.WriteLine($"[ERROR] Conflict: An operation conflict occurred with resource {resourceId}.");
            }
            else
            {
                Console.WriteLine($"[ERROR] Azure Storage operation failed: {ex.Message}");
            }
            
            Console.ResetColor();
            
            // Log the detailed error
            var errorDetails = new StringBuilder();
            errorDetails.AppendLine($"{ex.Message}");
            // Using Status and ErrorCode which are still available in the current SDK
            errorDetails.AppendLine($"Time:{DateTime.UtcNow}");
            errorDetails.AppendLine($"Status: {ex.Status} ({ex.ErrorCode})");
            
            if (!string.IsNullOrEmpty(ex.Message))
            {
                errorDetails.AppendLine();
                errorDetails.AppendLine("Exception Details:");
                errorDetails.AppendLine(ex.ToString());
            }
            
            // Log the error
            Logger.LogError($"Azure Storage error ({operationName}): {errorDetails}");
        }
    }
}

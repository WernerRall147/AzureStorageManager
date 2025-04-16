using System;
using System.Net;
using Microsoft.Extensions.Configuration;
using AzureStorageManager.Services;

namespace AzureStorageManager.Utilities
{
    /// <summary>
    /// Global proxy configurator that ensures proxy settings are applied early
    /// </summary>
    public static class GlobalProxyInitializer
    {
        private static bool _isInitialized = false;
        
        /// <summary>
        /// Initialize global proxy settings as early as possible
        /// </summary>
        public static void EnsureGlobalProxyConfigured(IConfiguration config)
        {
            // Only initialize once - check both our local flag and the ProxyService initialization status
            if (_isInitialized || ProxyService.IsInitialized)
                return;
                
            _isInitialized = true;
            
            Console.WriteLine("[PROXY] Ensuring proxy settings are configured globally before any connections...");
            
            // Call the ProxyService which has comprehensive proxy setup capabilities
            // Set the skipUserPrompt parameter to true to avoid prompting if config exists
            ProxyService.InitializeProxySettings(config, skipUserPrompt: true);
            
            // Log success
            Console.ForegroundColor = ConsoleColor.Green;
            if (ProxyService.IsProxyConfigured)
            {
                Console.WriteLine($"[PROXY] {ProxyService.ProxyInfo}");
            }
            else
            {
                Console.WriteLine("[PROXY] Global proxy configuration complete");
            }
            Console.ResetColor();
        }
    }
}

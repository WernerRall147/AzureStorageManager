using System;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using AzureStorageManager.Services;

namespace AzureStorageManager.Utilities
{
    /// <summary>
    /// Helper service for configuring and managing proxy settings
    /// </summary>
    public static class ProxyConfigHelper
    {        /// <summary>
        /// Initializes proxy settings from configuration or user input
        /// </summary>
        /// <param name="config">Application configuration</param>
        /// <returns>Configured HttpClient with proxy settings or null if no proxy is configured</returns>
        public static HttpClient? ConfigureProxySettings(IConfiguration? config)
        {
            // Check if proxy is already initialized to avoid duplicate prompts
            if (!ProxyService.IsInitialized)
            {
                // Use the centralized ProxyService to configure proxy settings
                ProxyService.InitializeProxySettings(config);
            }
            
            // Return the configured HttpClient from the ProxyService
            return ProxyService.HttpClient;
        }
    }
}

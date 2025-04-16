using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Configuration;

namespace AzureStorageManager.Services
{
    public static class ProxyService
    {
        private static HttpClient? _httpClient;
        private static bool _isInitialized = false;
        private static bool _isProxyConfigured = false;
        private static string _proxyInfo = "No proxy configured";
        
        public static HttpClient HttpClient => _httpClient ?? new HttpClient();        /// <summary>
        /// Returns whether a proxy is configured
        /// </summary>
        public static bool IsProxyConfigured => _isProxyConfigured;
        
        /// <summary>
        /// Returns whether proxy settings have already been initialized
        /// </summary>
        public static bool IsInitialized => _isInitialized;
        
        /// <summary>
        /// Returns a string with proxy information for display
        /// </summary>
        public static string ProxyInfo => _proxyInfo;
          /// <summary>
        /// Initializes proxy settings from configuration or prompts the user
        /// </summary>
        /// <param name="config">The application configuration</param>
        /// <param name="skipUserPrompt">Whether to skip prompting the user for proxy settings</param>
        public static void InitializeProxySettings(IConfiguration? config, bool skipUserPrompt = false)
        {
            // Only initialize once to avoid multiple prompts
            if (_isInitialized)
            {
                return;
            }
            
            _isInitialized = true;
            Console.WriteLine("[INFO] Checking proxy configuration...");
            
            bool useProxy = false;
            string proxyUrl = "";
            string proxyPort = "";
            string username = "";
            string password = "";
            
            // Check if proxy settings are in configuration
            if (config != null)
            {
                if (bool.TryParse(config["Proxy:UseProxy"], out bool configUseProxy))
                {
                    useProxy = configUseProxy;
                }
                
                proxyUrl = config["Proxy:ProxyUrl"] ?? "";
                proxyPort = config["Proxy:ProxyPort"] ?? "";
                username = config["Proxy:Username"] ?? "";
                password = config["Proxy:Password"] ?? "";
            }
            
            // If no proxy configured and user prompting is allowed, ask the user
            if (!useProxy && !skipUserPrompt)
            {
                Console.Write("Are you using a proxy? (yes/no): ");
                string? useProxyResponse = Console.ReadLine()?.Trim().ToLower();
                useProxy = useProxyResponse == "yes";
            }
            
            // If using proxy but details not complete, ask for them
            if (useProxy && (string.IsNullOrEmpty(proxyUrl) || string.IsNullOrEmpty(proxyPort)))
            {
                Console.WriteLine("[INFO] Proxy configuration is incomplete, please provide the details:");
                
                if (string.IsNullOrEmpty(proxyUrl))
                {
                    Console.Write("Enter the proxy URL: ");
                    proxyUrl = Console.ReadLine()?.Trim() ?? "";
                }
                
                if (string.IsNullOrEmpty(proxyPort))
                {
                    Console.Write("Enter the proxy port: ");
                    proxyPort = Console.ReadLine()?.Trim() ?? "";
                }
                
                Console.Write("Does your proxy require authentication? (yes/no): ");
                if ((Console.ReadLine()?.Trim().ToLower() ?? "") == "yes")
                {
                    if (string.IsNullOrEmpty(username))
                    {
                        Console.Write("Enter the proxy username: ");
                        username = Console.ReadLine()?.Trim() ?? "";
                    }
                    
                    if (string.IsNullOrEmpty(password))
                    {
                        Console.Write("Enter the proxy password: ");
                        password = ReadPasswordFromConsole();
                    }
                }
            }
              // Configure the proxy if needed
            if (useProxy && !string.IsNullOrEmpty(proxyUrl) && !string.IsNullOrEmpty(proxyPort) && int.TryParse(proxyPort, out int port))
            {
                var proxy = new WebProxy($"{proxyUrl}:{port}");
                
                // Add credentials if provided
                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                {
                    proxy.Credentials = new NetworkCredential(username, password);
                }
                
                // Configure global proxy settings
                WebRequest.DefaultWebProxy = proxy;
                
                // Also configure HttpClient for other connections
                var httpClientHandler = new HttpClientHandler
                {
                    Proxy = proxy,
                    UseProxy = true
                };
                _httpClient = new HttpClient(httpClientHandler);
                
                // Update proxy status for display
                _isProxyConfigured = true;
                _proxyInfo = $"Proxy: {proxyUrl}:{port}" + (!string.IsNullOrEmpty(username) ? " (authenticated)" : "");
                
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[INFO] Proxy configured: {proxyUrl}:{port}");
                Console.ResetColor();
                
                // Save the settings for future use if they came from user input
                if (config != null && string.IsNullOrEmpty(config["Proxy:ProxyUrl"]))
                {
                    SaveProxySettings(config, useProxy, proxyUrl, proxyPort, username, password);
                }
            }
            else if (useProxy)
            {
                _isProxyConfigured = false;
                _proxyInfo = "Invalid proxy configuration";
                
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[WARNING] Invalid proxy URL or port. Proceeding without proxy.");
                Console.ResetColor();            }
            else
            {
                _isProxyConfigured = false;
                _proxyInfo = "No proxy configured";
                
                Console.WriteLine("[INFO] No proxy configured.");
            }
        }
        
        /// <summary>
        /// Updates the proxy status for display in the UI
        /// </summary>
        /// <param name="isConfigured">Whether the proxy is configured</param>
        /// <param name="info">Information about the proxy configuration</param>
        public static void UpdateProxyStatus(bool isConfigured, string info)
        {
            _isProxyConfigured = isConfigured;
            _proxyInfo = info;
        }

        private static void SaveProxySettings(IConfiguration config, bool useProxy, string proxyUrl, string proxyPort, string username, string password)
        {
            try
            {
                var jsonConfig = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText("appsettings.json"));
                
                // Update the "Proxy" section
                var proxyConfig = new Dictionary<string, object>
                {
                    { "UseProxy", useProxy },
                    { "ProxyUrl", proxyUrl },
                    { "ProxyPort", proxyPort }
                };
                
                if (!string.IsNullOrEmpty(username))
                    proxyConfig["Username"] = username;
                    
                if (!string.IsNullOrEmpty(password))
                    proxyConfig["Password"] = password;
                    
                if (jsonConfig != null)
                {
                    jsonConfig["Proxy"] = proxyConfig;
                    string updatedJson = System.Text.Json.JsonSerializer.Serialize(jsonConfig, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText("appsettings.json", updatedJson);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARNING] Could not save proxy settings: {ex.Message}");
            }
        }

        private static string ReadPasswordFromConsole()
        {
            string password = "";
            ConsoleKeyInfo key;
            
            do
            {
                key = Console.ReadKey(true);
                
                // Ignore any control keys like CTRL, ALT, etc.
                if (!char.IsControl(key.KeyChar))
                {
                    password += key.KeyChar;
                    Console.Write("*");
                }
                // Handle backspace
                else if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                {
                    password = password.Substring(0, password.Length - 1);
                    Console.Write("\b \b");
                }
            }
            while (key.Key != ConsoleKey.Enter);
            
            Console.WriteLine();
            return password;
        }
    }
}

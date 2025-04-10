using System;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Files.Shares;

namespace AzureStorageManager.Models
{
    /// <summary>
    /// Maintains state for storage account connections across operations
    /// </summary>
    public static class ConnectionState
    {
        // Storage account connection info
        public static string? StorageAccountName { get; set; }
        public static TokenCredential? Credential { get; set; }
        
        // Blob storage info
        public static string? BlobContainerName { get; set; }
        public static BlobServiceClient? BlobServiceClient { get; set; }
        
        // File share info
        public static string? FileShareName { get; set; }
        public static string? FileShareFolderPath { get; set; }
        public static ShareServiceClient? ShareServiceClient { get; set; }
        
        /// <summary>
        /// Resets all connection state
        /// </summary>
        public static void Reset()
        {
            StorageAccountName = null;
            BlobContainerName = null;
            FileShareName = null;
            FileShareFolderPath = null;
            BlobServiceClient = null;
            ShareServiceClient = null;
        }
        
        /// <summary>
        /// Resets only the container/share specific connection state but keeps the storage account connection
        /// </summary>
        public static void ResetContainerState()
        {
            BlobContainerName = null;
            FileShareName = null;
            FileShareFolderPath = null;
        }
        
        /// <summary>
        /// Check if we have an active storage account connection
        /// </summary>
        public static bool IsStorageAccountConnected => !string.IsNullOrEmpty(StorageAccountName) && 
                                                       (BlobServiceClient != null || ShareServiceClient != null);
                                                       
        /// <summary>
        /// Returns a formatted string with the current connection status
        /// </summary>
        public static string GetConnectionInfoString()
        {
            if (!IsStorageAccountConnected)
                return "No storage account connected";
                
            string info = $"Connected to: {StorageAccountName}";
            
            if (!string.IsNullOrEmpty(BlobContainerName))
                info += $" | Container: {BlobContainerName}";
                
            if (!string.IsNullOrEmpty(FileShareName))
                info += $" | Share: {FileShareName}";
                
            return info;
        }
    }
}

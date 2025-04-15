using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;

namespace AzureStorageManager.Models
{
    /// <summary>
    /// Represents a directory or file item in an Azure File Share directory listing
    /// </summary>
    public class ShareDirectoryItem
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public bool IsDirectory { get; set; }
        public long? Size { get; set; }
        public DateTime? LastModified { get; set; }

        public ShareDirectoryItem(string name, string path, bool isDirectory, long? size = null, DateTime? lastModified = null)
        {
            Name = name;
            Path = path;
            IsDirectory = isDirectory;
            Size = size;
            LastModified = lastModified;
        }

        public override string ToString()
        {
            string displaySize = Size.HasValue ? $"{FormatFileSize(Size.Value)}" : "-";
            string displayDate = LastModified.HasValue ? LastModified.Value.ToString("yyyy-MM-dd HH:mm:ss") : "-";
            string itemType = IsDirectory ? "[DIR]" : "[FILE]";
            
            return $"{itemType,-6} {Name,-30} {displaySize,-10} {displayDate}";
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }
    }
}

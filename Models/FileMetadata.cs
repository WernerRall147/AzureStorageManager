namespace AzureStorageManager.Models
{
    public class FileMetadata
    {
        public string FileName { get; set; }
        public string LocalHash { get; set; }
        public string RemoteHash { get; set; }
        public string Status { get; set; }
        public string? LocalPath { get; set; } // Make nullable
        public string? RemotePath { get; set; } // Make nullable
        public long SizeInBytes { get; set; }

        // Constructor that sets all properties, including a custom status.
        public FileMetadata(string fileName, string localHash, string remoteHash, string status)
        {
            FileName = fileName;
            LocalHash = localHash;
            RemoteHash = remoteHash;
            Status = status;
            LocalPath = "";  // Empty string instead of null
            RemotePath = ""; // Empty string instead of null
            SizeInBytes = 0;
        }
        
        // Enhanced constructor with additional metadata
        public FileMetadata(string fileName, string localHash, string remoteHash, string status, 
                          string localPath, string remotePath, long sizeInBytes)
        {
            FileName = fileName;
            LocalHash = localHash;
            RemoteHash = remoteHash;
            Status = status;
            LocalPath = localPath;
            RemotePath = remotePath;
            SizeInBytes = sizeInBytes;
        }

        // Optional: If you often need a "Match"/"Mismatch" status based solely on hash comparison,
        // you can still provide a helper constructor or method. For example:
        public static FileMetadata FromHashes(string fileName, string localHash, string remoteHash)
        {
            string status = (localHash == remoteHash) ? "Match" : "Mismatch";
            return new FileMetadata(fileName, localHash, remoteHash, status);
        }
    }
}

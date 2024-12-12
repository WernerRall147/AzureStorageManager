namespace AzureStorageManager.Models
{
    public class FileMetadata
    {
        public string FileName { get; set; }
        public string LocalHash { get; set; }
        public string RemoteHash { get; set; }
        public string Status { get; set; }

        // Constructor that sets all properties, including a custom status.
        public FileMetadata(string fileName, string localHash, string remoteHash, string status)
        {
            FileName = fileName;
            LocalHash = localHash;
            RemoteHash = remoteHash;
            Status = status;
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

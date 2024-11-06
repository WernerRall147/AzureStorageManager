namespace AzureStorageManager.Models
{
    public class FileMetadata
    {
        public string FileName { get; set; }
        public string LocalHash { get; set; }
        public string RemoteHash { get; set; }
        public bool IsHashMatch => LocalHash == RemoteHash;
        public string Status => IsHashMatch ? "Match" : "Mismatch";

        // Constructor to initialize properties
        public FileMetadata(string fileName, string localHash, string remoteHash)
        {
            FileName = fileName;
            LocalHash = localHash;
            RemoteHash = remoteHash;
        }
    }
}

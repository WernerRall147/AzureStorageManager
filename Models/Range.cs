namespace AzureStorageManager.Models
{
    public class Range
    {
        public long Offset { get; set; }
        public long Length { get; set; }

        public Range(long offset, long length)
        {
            Offset = offset;
            Length = length;
        }
    }
}

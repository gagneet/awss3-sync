namespace S3FileManager.Models
{
    public class LocalFileInfo
    {
        public string RelativePath { get; set; } = string.Empty;
        public long Size { get; set; }
        public string FullPath { get; set; } = string.Empty; // Added FullPath as it's useful
    }
}

namespace S3FileManager.Models
{
    public enum ComparisonStatus
    {
        Identical,
        Modified,
        LocalOnly,
        S3Only,
        Directory
    }

    public class FileComparisonResult
    {
        public string RelativePath { get; set; }
        public string FileName { get; set; }
        public ComparisonStatus Status { get; set; }
        public FileNode LocalFile { get; set; }
        public FileNode S3File { get; set; }
    }
}

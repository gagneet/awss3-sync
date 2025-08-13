namespace S3FileManager.Models
{
    public class AppConfig
    {
        public AwsConfig AWS { get; set; } = new AwsConfig();
        public CognitoConfig Cognito { get; set; } = new CognitoConfig();
        public PerformanceConfig Performance { get; set; } = new PerformanceConfig();
    }

    public class AwsConfig
    {
        public string AccessKey { get; set; } = string.Empty;
        public string SecretKey { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public string BucketName { get; set; } = string.Empty;
    }
    
    public class PerformanceConfig
    {
        public int MaxConcurrentUploads { get; set; } = 5;
        public int MaxConcurrentDownloads { get; set; } = 5;
        public long ChunkSizeBytes { get; set; } = 5 * 1024 * 1024; // 5MB chunks
        public bool EnableMetadataCache { get; set; } = true;
        public int MetadataCacheDurationMinutes { get; set; } = 5;
        public bool EnableDeltaSync { get; set; } = true;
        public int SyncBatchSize { get; set; } = 100;
    }
}
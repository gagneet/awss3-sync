namespace FileSyncApp.Core.Models
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

    public class CognitoConfig
    {
        public string Region { get; set; } = string.Empty;
        public string UserPoolId { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string IdentityPoolId { get; set; } = string.Empty;
        public bool EnableOfflineMode { get; set; } = true;
        public int OfflineCacheDurationDays { get; set; } = 7;
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
        public long MaxBytesPerSecond { get; set; } = 0; // 0 means no limit
    }
}
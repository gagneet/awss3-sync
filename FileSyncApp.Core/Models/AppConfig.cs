using FileSyncApp.Core.Interfaces;

namespace FileSyncApp.Core.Models
{
    public class AppConfig
    {
        public AwsConfig AWS { get; set; } = new AwsConfig();
        public CognitoConfig Cognito { get; set; } = new CognitoConfig();
        public PerformanceConfig Performance { get; set; } = new PerformanceConfig();

        /// <summary>Named bucket profiles. If empty, the legacy AWS block is used as the default.</summary>
        public List<BucketProfile> Profiles { get; set; } = new();

        /// <summary>Name of the currently active profile. Empty = use legacy AWS block.</summary>
        public string ActiveProfileName { get; set; } = string.Empty;

        /// <summary>Scheduled sync jobs.</summary>
        public List<SyncSchedule> Schedules { get; set; } = new();
    }

    public class AwsConfig
    {
        public string AccessKey { get; set; } = string.Empty;
        public string SecretKey { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public string BucketName { get; set; } = string.Empty;
    }

    /// <summary>A named AWS bucket connection profile.</summary>
    public class BucketProfile
    {
        public string Name       { get; set; } = string.Empty;
        public string AccessKey  { get; set; } = string.Empty;
        public string SecretKey  { get; set; } = string.Empty;
        public string Region     { get; set; } = string.Empty;
        public string BucketName { get; set; } = string.Empty;
    }

    /// <summary>A scheduled background sync job.</summary>
    public class SyncSchedule
    {
        public string         Id             { get; set; } = Guid.NewGuid().ToString("N");
        public string         Name           { get; set; } = string.Empty;
        public string         LocalPath      { get; set; } = string.Empty;
        public string         RemotePrefix   { get; set; } = string.Empty;
        /// <summary>Cron expression (Quartz format). E.g. "0 0 * * * ?" = every hour.</summary>
        public string         CronExpression { get; set; } = "0 0 * * * ?";
        public bool           IsEnabled      { get; set; } = true;
        public ConflictPolicy ConflictPolicy { get; set; } = ConflictPolicy.NewerWins;
        public DateTime?      LastRun        { get; set; }
        public DateTime?      NextRun        { get; set; }
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
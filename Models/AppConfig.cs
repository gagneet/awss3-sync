namespace AWSS3Sync.Models
{
    public class AppConfig
    {
        public AwsConfig AWS { get; set; } = new AwsConfig();
    }

    public class AwsConfig
    {
        public string AccessKey { get; set; } = string.Empty;
        public string SecretKey { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public string BucketName { get; set; } = string.Empty;
    }
}
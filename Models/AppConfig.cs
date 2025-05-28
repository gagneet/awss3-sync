namespace S3FileManager.Models
{
    public class AppConfig
    {
        public AwsConfig AWS { get; set; }
    }

    public class AwsConfig
    {
        public string AccessKey { get; set; }
        public string SecretKey { get; set; }
        public string Region { get; set; }
        public string BucketName { get; set; }
    }
}
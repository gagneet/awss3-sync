using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using FileSyncApp.Core.Interfaces;
using FileSyncApp.Core.Models;
using Microsoft.Extensions.Logging;

namespace FileSyncApp.S3.Services;

public class S3FileStorageService : IFileStorageService, IDisposable
{
    private readonly IAuthService _authService;
    private readonly IConfigurationService _configService;
    private readonly ILogger<S3FileStorageService> _logger;
    private readonly ILogger<S3MetadataService> _metadataLogger;

    private IAmazonS3? _s3Client;
    private TransferUtility? _transferUtility;
    private readonly SemaphoreSlim _transferSemaphore;
    private long _maxBytesPerSecond;
    private string _lastAccessKey = string.Empty;
    private bool _isInitialized = false;

    public S3FileStorageService(
        IAuthService authService,
        IConfigurationService configService,
        ILogger<S3FileStorageService> logger,
        ILogger<S3MetadataService> metadataLogger)
    {
        _authService = authService;
        _configService = configService;
        _logger = logger;
        _metadataLogger = metadataLogger;

        var config = _configService.GetConfiguration();
        _transferSemaphore = new SemaphoreSlim(config.Performance.MaxConcurrentUploads);
        _maxBytesPerSecond = config.Performance.MaxBytesPerSecond;
    }

    private IAmazonS3 GetClient()
    {
        var user = _authService.GetCurrentUser();
        var config = _configService.GetConfiguration();

        if (string.IsNullOrEmpty(config.AWS.BucketName))
        {
            throw new InvalidOperationException("BucketName is not configured in appsettings.json. Please set AWS:BucketName.");
        }

        if (string.IsNullOrEmpty(config.AWS.Region))
        {
            throw new InvalidOperationException("Region is not configured in appsettings.json. Please set AWS:Region.");
        }

        string currentAccessKey = user?.AwsAccessKeyId ?? config.AWS.AccessKey;

        if (_s3Client != null && _lastAccessKey == currentAccessKey && _isInitialized)
        {
            return _s3Client;
        }

        _s3Client?.Dispose();
        _transferUtility?.Dispose();

        var s3Config = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(config.AWS.Region),
            Timeout = TimeSpan.FromSeconds(30),
            MaxErrorRetry = 3
        };

        if (user != null && user.HasAwsCredentials)
        {
            var credentials = new Amazon.Runtime.SessionAWSCredentials(
                user.AwsAccessKeyId,
                user.AwsSecretAccessKey,
                user.AwsSessionToken);
            _s3Client = new AmazonS3Client(credentials, s3Config);
            _logger.LogInformation("S3 client initialized with user session credentials");
        }
        else if (!string.IsNullOrEmpty(config.AWS.AccessKey) && !string.IsNullOrEmpty(config.AWS.SecretKey))
        {
            _s3Client = new AmazonS3Client(config.AWS.AccessKey, config.AWS.SecretKey, s3Config);
            _logger.LogInformation("S3 client initialized with config credentials");
        }
        else
        {
            throw new InvalidOperationException("No AWS credentials available. Please configure AWS:AccessKey and AWS:SecretKey in appsettings.json or authenticate with Cognito.");
        }

        _transferUtility = new TransferUtility(_s3Client);
        _lastAccessKey = currentAccessKey;
        _isInitialized = true;

        return _s3Client;
    }

    public async Task<List<FileNode>> ListFilesAsync(UserRole userRole, string prefix = "", CancellationToken cancellationToken = default)
    {
        var client = GetClient();
        var config = _configService.GetConfiguration();
        var bucketName = config.AWS.BucketName;

        _logger.LogInformation("Listing files in bucket {Bucket} with prefix '{Prefix}'", bucketName, prefix);

        var files = new List<FileNode>();
        string? continuationToken = null;

        // Create a timeout for the entire operation
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            do
            {
                var request = new ListObjectsV2Request
                {
                    BucketName = bucketName,
                    Prefix = prefix,
                    Delimiter = "/", // Only get immediate children, not recursive
                    ContinuationToken = continuationToken,
                    MaxKeys = 200 // Limit batch size for responsiveness
                };

                var response = await client.ListObjectsV2Async(request, linkedCts.Token);

                // Add directories (common prefixes)
                foreach (var commonPrefix in response.CommonPrefixes)
                {
                    if (string.IsNullOrEmpty(commonPrefix)) continue;
                    
                    var dirName = commonPrefix.TrimEnd('/');
                    if (dirName.Contains('/'))
                        dirName = dirName.Substring(dirName.LastIndexOf('/') + 1);

                    var node = new FileNode(
                        dirName,
                        commonPrefix,
                        true,
                        0,
                        DateTime.MinValue,
                        new List<UserRole> { UserRole.Administrator, UserRole.Executive, UserRole.User });
                    
                    files.Add(node);
                }

                // Add files
                foreach (var obj in response.S3Objects)
                {
                    // Skip the prefix itself if it appears as an object
                    if (obj.Key == prefix || obj.Key.EndsWith("/")) continue;

                    var fileName = Path.GetFileName(obj.Key);
                    if (string.IsNullOrEmpty(fileName)) continue;

                    // For performance, don't fetch metadata for each file during listing
                    // Just use default access roles
                    var accessRoles = new List<UserRole> { UserRole.Administrator, UserRole.Executive, UserRole.User };

                    var node = new FileNode(
                        fileName,
                        obj.Key,
                        false,
                        obj.Size ?? 0,
                        obj.LastModified ?? DateTime.MinValue,
                        accessRoles);

                    if (CanUserAccessFile(userRole, node))
                    {
                        files.Add(node);
                    }
                }

                continuationToken = response.NextContinuationToken;
                
                // Yield control periodically for UI responsiveness
                if (!string.IsNullOrEmpty(continuationToken))
                {
                    await Task.Yield();
                }

            } while (!string.IsNullOrEmpty(continuationToken) && !linkedCts.Token.IsCancellationRequested);

            _logger.LogInformation("Listed {Count} files from S3", files.Count);
            return files;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            _logger.LogWarning("S3 listing timed out after 60 seconds");
            throw new TimeoutException("S3 listing operation timed out. Please check your network connection.");
        }
    }

    public async Task<bool> UploadFileAsync(string filePath, string key, List<UserRole> accessRoles, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        await _transferSemaphore.WaitAsync(cancellationToken);
        try
        {
            var client = GetClient();
            var config = _configService.GetConfiguration();
            var bucketName = config.AWS.BucketName;
            var metadataService = new S3MetadataService(client, bucketName, _metadataLogger);

            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var throttledStream = new FileSyncApp.Core.Services.ThrottledStream(fileStream, _maxBytesPerSecond);

            var uploadRequest = new TransferUtilityUploadRequest
            {
                BucketName = bucketName,
                Key = key,
                InputStream = throttledStream
            };

            if (progress != null)
            {
                uploadRequest.UploadProgressEvent += (s, e) => progress.Report((double)e.TransferredBytes / e.TotalBytes * 100);
            }

            await _transferUtility!.UploadAsync(uploadRequest, cancellationToken);
            await metadataService.SetFileAccessRolesAsync(key, accessRoles);
            return true;
        }
        finally
        {
            _transferSemaphore.Release();
        }
    }

    public async Task DownloadFileAsync(string s3Key, string localRootPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        await _transferSemaphore.WaitAsync(cancellationToken);
        try
        {
            var client = GetClient();
            var config = _configService.GetConfiguration();
            var bucketName = config.AWS.BucketName;

            var fullPath = Path.Combine(localRootPath, s3Key.Replace("/", "\\"));
            var directory = Path.GetDirectoryName(fullPath);
            if (directory != null) Directory.CreateDirectory(directory);

            var getRequest = new GetObjectRequest
            {
                BucketName = bucketName,
                Key = s3Key
            };

            using var response = await client.GetObjectAsync(getRequest, cancellationToken);
            using var responseStream = response.ResponseStream;
            using var throttledStream = new FileSyncApp.Core.Services.ThrottledStream(responseStream, _maxBytesPerSecond);

            using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await throttledStream.CopyToAsync(fileStream, 81920, cancellationToken);

            File.SetLastWriteTimeUtc(fullPath, (response.LastModified ?? DateTime.UtcNow).ToUniversalTime());
        }
        finally
        {
            _transferSemaphore.Release();
        }
    }

    public async Task DeleteFileAsync(string s3Key, CancellationToken cancellationToken = default)
    {
        var client = GetClient();
        var config = _configService.GetConfiguration();
        await client.DeleteObjectAsync(config.AWS.BucketName, s3Key, cancellationToken);
    }

    private bool CanUserAccessFile(UserRole userRole, FileNode node)
    {
        if (userRole == UserRole.Administrator) return true;
        if (userRole == UserRole.Executive)
            return node.AccessRoles.Contains(UserRole.Executive) || node.AccessRoles.Contains(UserRole.Administrator);
        if (userRole == UserRole.User)
            return node.AccessRoles.Contains(UserRole.User);
        return false;
    }

    public void Dispose()
    {
        _s3Client?.Dispose();
        _transferUtility?.Dispose();
        _transferSemaphore.Dispose();
    }
}

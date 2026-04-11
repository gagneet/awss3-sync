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
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, (List<FileNode> Items, DateTime FetchedAt)> _listingCache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

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

        var cacheKey = $"{prefix}:{userRole}";
        if (_listingCache.TryGetValue(cacheKey, out var cached) && DateTime.UtcNow - cached.FetchedAt < CacheTtl)
        {
            _logger.LogDebug("S3 listing cache hit for {Prefix}", prefix);
            return cached.Items;
        }

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

                // Add directories (common prefixes) - null check required as AWS SDK can return null
                var commonPrefixes = response.CommonPrefixes;
                if (commonPrefixes != null)
                {
                    foreach (var commonPrefix in commonPrefixes)
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
                }

                // Add files - null check required as AWS SDK can return null for empty buckets
                var s3Objects = response.S3Objects;
                if (s3Objects != null)
                {
                    foreach (var obj in s3Objects)
                    {
                        if (obj == null) continue;
                        
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
                }
                else
                {
                    _logger.LogDebug("S3Objects collection was null for prefix '{Prefix}'", prefix);
                }

                continuationToken = response.NextContinuationToken;
                
                // Yield control periodically for UI responsiveness
                if (!string.IsNullOrEmpty(continuationToken))
                {
                    await Task.Yield();
                }

            } while (!string.IsNullOrEmpty(continuationToken) && !linkedCts.Token.IsCancellationRequested);

            _logger.LogInformation("Listed {Count} files from S3", files.Count);
            _listingCache[cacheKey] = (files, DateTime.UtcNow);
            return files;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            _logger.LogWarning("S3 listing timed out after 60 seconds");
            throw new TimeoutException("S3 listing operation timed out. Please check your network connection.");
        }
        catch (AmazonS3Exception ex) when (!timeoutCts.IsCancellationRequested)
        {
            _logger.LogError(ex, "S3 listing failed for prefix {Prefix}", prefix);
            throw new InvalidOperationException(FriendlyS3Error(ex), ex);
        }
    }

    public async Task<List<FileNode>> ListAllFilesRecursiveAsync(UserRole userRole, string prefix = "", CancellationToken cancellationToken = default)
    {
        var client = GetClient();
        var config = _configService.GetConfiguration();
        var bucketName = config.AWS.BucketName;
        var files = new List<FileNode>();
        string? continuationToken = null;

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            do
            {
                // No Delimiter — returns every object at every depth below prefix
                var request = new ListObjectsV2Request
                {
                    BucketName        = bucketName,
                    Prefix            = prefix,
                    ContinuationToken = continuationToken,
                    MaxKeys           = 1000
                };

                var response = await client.ListObjectsV2Async(request, linkedCts.Token);

                if (response.S3Objects != null)
                {
                    foreach (var obj in response.S3Objects)
                    {
                        if (obj == null || obj.Key == prefix || obj.Key.EndsWith("/")) continue;
                        var fileName = Path.GetFileName(obj.Key);
                        if (string.IsNullOrEmpty(fileName)) continue;

                        var node = new FileNode(
                            fileName,
                            obj.Key,
                            false,
                            obj.Size ?? 0,
                            obj.LastModified ?? DateTime.MinValue,
                            new List<UserRole> { UserRole.Administrator, UserRole.Executive, UserRole.User });

                        if (CanUserAccessFile(userRole, node))
                            files.Add(node);
                    }
                }

                continuationToken = response.NextContinuationToken;
                if (!string.IsNullOrEmpty(continuationToken))
                    await Task.Yield();

            } while (!string.IsNullOrEmpty(continuationToken) && !linkedCts.Token.IsCancellationRequested);

            return files;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            _logger.LogWarning("Recursive S3 listing timed out");
            throw new TimeoutException("S3 recursive listing timed out. Please check your network connection.");
        }
        catch (AmazonS3Exception ex) when (!timeoutCts.IsCancellationRequested)
        {
            _logger.LogError(ex, "S3 recursive listing failed for prefix {Prefix}", prefix);
            throw new InvalidOperationException(FriendlyS3Error(ex), ex);
        }
    }

    public async Task<bool> UploadFileAsync(string filePath, string key, List<UserRole> accessRoles, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        await _transferSemaphore.WaitAsync(cancellationToken);
        try
        {
            var client = GetClient();
            // Guard against edge-case races where _transferUtility could be null
            _transferUtility ??= new TransferUtility(client);
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
                uploadRequest.UploadProgressEvent += (s, e) =>
                {
                    var total = e.TotalBytes;
                    if (total > 0) progress.Report((double)e.TransferredBytes / total * 100);
                };
            }

            try
            {
                await _transferUtility.UploadAsync(uploadRequest, cancellationToken);
                await metadataService.SetFileAccessRolesAsync(key, accessRoles);
                _listingCache.Clear();
                return true;
            }
            catch (AmazonS3Exception ex)
            {
                _logger.LogError(ex, "Upload failed for {Key}", key);
                throw new InvalidOperationException(FriendlyS3Error(ex), ex);
            }
        }
        finally
        {
            _transferSemaphore.Release();
        }
    }

    public async Task DownloadFileAsync(string s3Key, string localFilePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        await _transferSemaphore.WaitAsync(cancellationToken);
        try
        {
            var client = GetClient();
            var config = _configService.GetConfiguration();
            var bucketName = config.AWS.BucketName;

            // localFilePath is already the full destination path — do not re-combine with s3Key
            var fullPath = localFilePath;
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var getRequest = new GetObjectRequest
            {
                BucketName = bucketName,
                Key = s3Key
            };

            try
            {
                using var response = await client.GetObjectAsync(getRequest, cancellationToken);
                using var responseStream = response.ResponseStream;
                using var throttledStream = new FileSyncApp.Core.Services.ThrottledStream(responseStream, _maxBytesPerSecond);

                using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await throttledStream.CopyToAsync(fileStream, 81920, cancellationToken);

                File.SetLastWriteTimeUtc(fullPath, (response.LastModified ?? DateTime.UtcNow).ToUniversalTime());
                _listingCache.Clear();
            }
            catch (AmazonS3Exception ex)
            {
                _logger.LogError(ex, "Download failed for {Key}", s3Key);
                throw new InvalidOperationException(FriendlyS3Error(ex), ex);
            }
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

    public async Task DownloadAsZipAsync(IEnumerable<string> s3Keys, string zipFilePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var client = GetClient();
        var config = _configService.GetConfiguration();
        var zipService = new S3ZipService(client, config.AWS.BucketName);
        using var fileStream = System.IO.File.Create(zipFilePath);
        await zipService.CreateZipFromS3Async(s3Keys, fileStream);
        progress?.Report(100.0);
    }

    public async Task DownloadFolderAsync(string s3Prefix, string localBasePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var user = _authService.GetCurrentUser();
        var files = await ListFilesAsync(user?.Role ?? UserRole.User, s3Prefix, cancellationToken);
        var total = files.Count;
        var completed = 0;

        foreach (var file in files)
        {
            var relative = file.Path.StartsWith(s3Prefix) ? file.Path[s3Prefix.Length..].TrimStart('/') : file.Path;
            var localPath = System.IO.Path.Combine(localBasePath, relative.Replace('/', System.IO.Path.DirectorySeparatorChar));
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(localPath)!);
            await DownloadFileAsync(file.Path, localPath, null, cancellationToken);
            completed++;
            progress?.Report(total > 0 ? (double)completed / total * 100 : 0);
        }
    }

    public async Task DeleteFilesAsync(IEnumerable<string> s3Keys, CancellationToken cancellationToken = default)
    {
        var client = GetClient();
        var config = _configService.GetConfiguration();
        var keyList = s3Keys.ToList();

        // Process in batches of 1000 (S3 API limit)
        for (int i = 0; i < keyList.Count; i += 1000)
        {
            var batch = keyList.Skip(i).Take(1000).ToList();
            var request = new DeleteObjectsRequest
            {
                BucketName = config.AWS.BucketName,
                Objects = batch.Select(k => new KeyVersion { Key = k }).ToList()
            };
            try
            {
                var response = await client.DeleteObjectsAsync(request, cancellationToken);
                if (response.DeleteErrors?.Count > 0)
                {
                    var errors = string.Join(", ", response.DeleteErrors.Select(e => $"{e.Key}: {e.Message}"));
                    _logger.LogWarning("Batch delete had {Count} error(s): {Errors}", response.DeleteErrors.Count, errors);
                }
            }
            catch (AmazonS3Exception ex)
            {
                _logger.LogError(ex, "Batch delete failed for {Count} keys", batch.Count);
                throw new InvalidOperationException(FriendlyS3Error(ex), ex);
            }
        }
        _listingCache.Clear();
    }

    public Task<string?> GetPresignedUrlAsync(string s3Key, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        var client = GetClient();
        var config = _configService.GetConfiguration();
        var request = new GetPreSignedUrlRequest
        {
            BucketName = config.AWS.BucketName,
            Key = s3Key,
            Expires = DateTime.UtcNow.Add(expiry)
        };
        return Task.FromResult<string?>(client.GetPreSignedURL(request));
    }

    public void InvalidateListingCache(string? prefix = null)
    {
        if (prefix == null)
        {
            _listingCache.Clear();
        }
        else
        {
            foreach (var key in _listingCache.Keys.Where(k => k.StartsWith(prefix)))
                _listingCache.TryRemove(key, out _);
        }
    }

    private static string FriendlyS3Error(AmazonS3Exception ex) => ex.ErrorCode switch
    {
        "AccessDenied"          => "Access denied. Check your AWS credentials and bucket permissions.",
        "NoSuchBucket"          => "The S3 bucket does not exist. Verify BucketName in appsettings.json.",
        "NoSuchKey"             => "The file no longer exists in S3.",
        "InvalidAccessKeyId"    => "Invalid AWS Access Key. Check your credentials.",
        "SignatureDoesNotMatch" => "Invalid AWS Secret Key. Check your credentials.",
        "RequestTimeout"        => "The request timed out. Check your network connection.",
        "ServiceUnavailable"    => "AWS S3 is temporarily unavailable. Try again later.",
        _                       => $"S3 error ({ex.ErrorCode}): {ex.Message}"
    };

    public void Dispose()
    {
        _s3Client?.Dispose();
        _transferUtility?.Dispose();
        _transferSemaphore.Dispose();
    }
}

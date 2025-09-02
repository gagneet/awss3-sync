using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using S3FileManager.Models;
using CognitoUserModel = S3FileManager.Models.CognitoUser;

namespace S3FileManager.Services
{
    /// <summary>
    /// Optimized S3 service with improved performance for uploads, downloads, and sync operations
    /// </summary>
    public class OptimizedS3Service : IDisposable
    {
        private readonly AmazonS3Client _s3Client;
        private readonly string _bucketName;
        private readonly MetadataService _metadataService;
        private readonly PerformanceConfig _performanceConfig;
        private readonly TransferUtility _transferUtility;
        private readonly SemaphoreSlim _uploadSemaphore;
        private readonly SemaphoreSlim _downloadSemaphore;
        private readonly ConcurrentDictionary<string, S3ObjectMetadata> _metadataCache;
        private readonly System.Threading.Timer _cacheCleanupTimer;
        private readonly bool _hasValidCredentials;
        
        public OptimizedS3Service(CognitoUserModel? cognitoUser = null, UnifiedUser? unifiedUser = null)
        {
            var config = ConfigurationService.GetConfiguration();
            _performanceConfig = config.Performance;
            
            // Determine which user to use (prefer unifiedUser)
            var userToUse = unifiedUser ?? (cognitoUser != null ? UnifiedUser.FromCognitoUser(cognitoUser) : null);
            
            // Create S3 client with user credentials if available
            if (userToUse?.HasAwsCredentials == true)
            {
                // Use temporary credentials from authenticated user
                var credentials = new Amazon.Runtime.SessionAWSCredentials(
                    userToUse.AwsAccessKeyId!,
                    userToUse.AwsSecretAccessKey!,
                    userToUse.AwsSessionToken);
                
                _s3Client = new AmazonS3Client(credentials, 
                    RegionEndpoint.GetBySystemName(config.AWS.Region));
                _hasValidCredentials = true;
            }
            else
            {
                // Fall back to configured credentials
                var awsConfig = new AmazonS3Config
                {
                    RegionEndpoint = RegionEndpoint.GetBySystemName(config.AWS.Region),
                    MaxErrorRetry = 3,
                    Timeout = TimeSpan.FromMinutes(5),
                    ReadWriteTimeout = TimeSpan.FromMinutes(5)
                };
                
                _s3Client = new AmazonS3Client(
                    config.AWS.AccessKey, 
                    config.AWS.SecretKey, 
                    awsConfig);
                _hasValidCredentials = !string.IsNullOrEmpty(config.AWS.AccessKey) && !string.IsNullOrEmpty(config.AWS.SecretKey);
            }
            
            _bucketName = config.AWS.BucketName;
            _metadataService = new MetadataService();
            
            // Initialize transfer utility for optimized transfers
            var transferConfig = new TransferUtilityConfig
            {
                ConcurrentServiceRequests = 10,
                MinSizeBeforePartUpload = _performanceConfig.ChunkSizeBytes
            };
            _transferUtility = new TransferUtility(_s3Client, transferConfig);
            
            // Initialize semaphores for concurrency control
            _uploadSemaphore = new SemaphoreSlim(_performanceConfig.MaxConcurrentUploads);
            _downloadSemaphore = new SemaphoreSlim(_performanceConfig.MaxConcurrentDownloads);
            
            // Initialize metadata cache
            _metadataCache = new ConcurrentDictionary<string, S3ObjectMetadata>();
            
            // Setup cache cleanup timer
            _cacheCleanupTimer = new System.Threading.Timer(
                CleanupCache, 
                null, 
                TimeSpan.FromMinutes(_performanceConfig.MetadataCacheDurationMinutes),
                TimeSpan.FromMinutes(_performanceConfig.MetadataCacheDurationMinutes));
        }
        
        /// <summary>
        /// Validate that the service has proper AWS credentials
        /// </summary>
        private void ValidateCredentials(string operation = "S3 operation")
        {
            if (!_hasValidCredentials)
            {
                throw new InvalidOperationException(
                    $"Cannot perform {operation}: No valid AWS credentials available. " +
                    "Please authenticate with AWS Cognito or configure AWS credentials.");
            }
        }
        
        /// <summary>
        /// List files with caching and pagination for better performance
        /// </summary>
        public async Task<List<S3FileItem>> ListFilesAsync(UserRole userRole, string prefix = "", 
            CancellationToken cancellationToken = default)
        {
            ValidateCredentials("list S3 files");
            var files = new List<S3FileItem>();
            string? continuationToken = null;
            
            do
            {
                var request = new ListObjectsV2Request
                {
                    BucketName = _bucketName,
                    Prefix = prefix,
                    MaxKeys = 1000,
                    ContinuationToken = continuationToken
                };
                
                var response = await _s3Client.ListObjectsV2Async(request, cancellationToken);
                
                // Process objects in parallel for better performance
                var tasks = response.S3Objects.Select(async obj =>
                {
                    var accessRoles = await _metadataService.GetFileAccessRolesAsync(obj.Key);
                    
                    return new S3FileItem
                    {
                        Key = obj.Key,
                        Size = obj.Size,
                        LastModified = obj.LastModified,
                        AccessRoles = accessRoles
                    };
                });
                
                var items = await Task.WhenAll(tasks);
                
                // Apply role-based filtering
                foreach (var item in items)
                {
                    if (CanUserAccessFile(userRole, item))
                    {
                        files.Add(item);
                    }
                }
                
                continuationToken = response.NextContinuationToken;
            } while (!string.IsNullOrEmpty(continuationToken) && !cancellationToken.IsCancellationRequested);
            
            // Filter out logs folder
            files = files.Where(f => !f.Key.StartsWith("logs/", StringComparison.OrdinalIgnoreCase)).ToList();
            
            return files;
        }
        
        /// <summary>
        /// Upload file with multipart upload for large files
        /// </summary>
        public async Task<bool> UploadFileAsync(string filePath, string key, 
            List<UserRole> accessRoles, IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            ValidateCredentials("upload file to S3");
            await _uploadSemaphore.WaitAsync(cancellationToken);
            
            try
            {
                if (!File.Exists(filePath)) throw new FileNotFoundException($"File not found: {filePath}");
                var fileInfo = new FileInfo(filePath);
                
                // Check if file needs to be uploaded using cached metadata
                if (_performanceConfig.EnableMetadataCache)
                {
                    var cachedMetadata = GetCachedMetadata(key);
                    if (cachedMetadata != null && 
                        cachedMetadata.LastModified >= fileInfo.LastWriteTimeUtc &&
                        cachedMetadata.Size == fileInfo.Length)
                    {
                        return false; // Skip upload
                    }
                }
                
                // Use multipart upload for large files
                if (fileInfo.Length > _performanceConfig.ChunkSizeBytes)
                {
                    var uploadRequest = new TransferUtilityUploadRequest
                    {
                        BucketName = _bucketName,
                        Key = key,
                        FilePath = filePath,
                        ContentType = GetContentType(filePath),
                        PartSize = _performanceConfig.ChunkSizeBytes,
                        StorageClass = S3StorageClass.Standard
                    };
                    
                    // Track upload progress
                    if (progress != null)
                    {
                        uploadRequest.UploadProgressEvent += (sender, args) =>
                        {
                            progress.Report((double)args.TransferredBytes / args.TotalBytes * 100);
                        };
                    }
                    
                    await _transferUtility.UploadAsync(uploadRequest, cancellationToken);
                }
                else
                {
                    // Use simple upload for small files
                    var putRequest = new PutObjectRequest
                    {
                        BucketName = _bucketName,
                        Key = key,
                        FilePath = filePath,
                        ContentType = GetContentType(filePath)
                    };
                    
                    await _s3Client.PutObjectAsync(putRequest, cancellationToken);
                }
                
                // Set access roles
                await _metadataService.SetFileAccessRolesAsync(key, accessRoles);
                
                // Update cache
                UpdateCachedMetadata(key, fileInfo.LastWriteTimeUtc, fileInfo.Length);
                
                return true;
            }
            finally
            {
                _uploadSemaphore.Release();
            }
        }
        
        /// <summary>
        /// Upload directory with parallel file uploads
        /// </summary>
        public async Task UploadDirectoryAsync(string directoryPath, string keyPrefix, 
            List<UserRole> accessRoles, IProgress<int>? progress = null,
            CancellationToken cancellationToken = default)
        {
            ValidateCredentials("upload directory to S3");
            var files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
            var uploadedCount = 0;
            
            // Create a channel for producer-consumer pattern
            var channel = Channel.CreateUnbounded<(string FilePath, string Key)>();
            
            // Producer task - queue files for upload
            var producerTask = Task.Run(async () =>
            {
                foreach (var file in files)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;
                    
                    var relativePath = Path.GetRelativePath(directoryPath, file);
                    var key = Path.Combine(keyPrefix, relativePath).Replace('\\', '/');
                    
                    await channel.Writer.WriteAsync((file, key), cancellationToken);
                }
                
                channel.Writer.Complete();
            }, cancellationToken);
            
            // Consumer tasks - upload files in parallel
            var consumerTasks = Enumerable.Range(0, _performanceConfig.MaxConcurrentUploads)
                .Select(async _ =>
                {
                    await foreach (var (filePath, key) in channel.Reader.ReadAllAsync(cancellationToken))
                    {
                        try
                        {
                            await UploadFileAsync(filePath, key, accessRoles, null, cancellationToken);
                            
                            var count = Interlocked.Increment(ref uploadedCount);
                            progress?.Report(count * 100 / files.Length);
                        }
                        catch (Exception ex)
                        {
                            // Log error but continue with other files
                            EventLog.WriteEntry("Application", $"Failed to upload {filePath}: {ex.Message}", EventLogEntryType.Error);
                        }
                    }
                });
            
            await Task.WhenAll(producerTask);
            await Task.WhenAll(consumerTasks);
        }
        
        /// <summary>
        /// Download file with resume support for large files
        /// </summary>
        public async Task DownloadFileAsync(string s3Key, string localPath, 
            IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            ValidateCredentials("download file from S3");
            await _downloadSemaphore.WaitAsync(cancellationToken);
            
            try
            {
                var fileName = Path.GetFileName(s3Key);
                var fullPath = Path.Combine(localPath, fileName);

                // Validate the file path
                if (!fullPath.StartsWith(Path.GetFullPath(localPath)) || fullPath.Contains(".."))
                {
                    throw new ArgumentException("Invalid file path detected.");
                }
                
                // Create directory if it doesn't exist
                Directory.CreateDirectory(localPath);
                
                // Get object metadata
                var metadataRequest = new GetObjectMetadataRequest
                {
                    BucketName = _bucketName,
                    Key = s3Key
                };
                
                var metadata = await _s3Client.GetObjectMetadataAsync(metadataRequest, cancellationToken);
                
                // Use transfer utility for optimized download
                if (metadata.ContentLength > _performanceConfig.ChunkSizeBytes)
                {
                    var downloadRequest = new TransferUtilityDownloadRequest
                    {
                        BucketName = _bucketName,
                        Key = s3Key,
                        FilePath = fullPath
                    };
                    
                    // Track download progress
                    if (progress != null)
                    {
                        downloadRequest.WriteObjectProgressEvent += (sender, args) =>
                        {
                            progress.Report((double)args.TransferredBytes / args.TotalBytes * 100);
                        };
                    }
                    
                    await _transferUtility.DownloadAsync(downloadRequest, cancellationToken);
                }
                else
                {
                    // Use simple download for small files
                    var getRequest = new GetObjectRequest
                    {
                        BucketName = _bucketName,
                        Key = s3Key
                    };
                    
                    using (var response = await _s3Client.GetObjectAsync(getRequest, cancellationToken))
                    using (var fileStream = File.Create(fullPath))
                    {
                        await response.ResponseStream.CopyToAsync(fileStream, 81920, cancellationToken);
                    }
                }
                
                // Set file timestamps to match S3
                File.SetLastWriteTimeUtc(fullPath, metadata.LastModified.ToUniversalTime());
            }
            finally
            {
                _downloadSemaphore.Release();
            }
        }
        
        /// <summary>
        /// Optimized sync with delta detection and parallel operations
        /// </summary>
        public async Task<SyncResult> SyncS3ToLocalAsync(string localPath, string s3Prefix,
            UserRole userRole, IProgress<SyncProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new SyncResult();
            var syncProgress = new SyncProgress();
            
            // Get S3 files
            syncProgress.Status = "Listing S3 files...";
            progress?.Report(syncProgress);
            
            var s3Files = await ListFilesAsync(userRole, s3Prefix, cancellationToken);
            var s3FileDict = s3Files.Where(f => !f.IsDirectory)
                .ToDictionary(f => f.Key, f => f);
            
            // Get local files
            syncProgress.Status = "Scanning local files...";
            progress?.Report(syncProgress);
            
            var localFiles = new Dictionary<string, FileInfo>();
            if (Directory.Exists(localPath))
            {
                foreach (var file in Directory.GetFiles(localPath, "*", SearchOption.AllDirectories))
                {
                    var relativePath = Path.GetRelativePath(localPath, file).Replace('\\', '/');
                    var s3Key = string.IsNullOrEmpty(s3Prefix) 
                        ? relativePath 
                        : Path.Combine(s3Prefix, relativePath).Replace('\\', '/');
                    localFiles[s3Key] = new FileInfo(file);
                }
            }
            else
            {
                Directory.CreateDirectory(localPath);
            }
            
            // Determine files to download, skip, and delete
            var toDownload = new List<S3FileItem>();
            var toDelete = new List<string>();
            
            // Check S3 files
            foreach (var s3File in s3FileDict.Values)
            {
                if (localFiles.TryGetValue(s3File.Key, out var localFile))
                {
                    // File exists locally - check if update needed
                    if (localFile.LastWriteTimeUtc < s3File.LastModified.ToUniversalTime() ||
                        localFile.Length != s3File.Size)
                    {
                        toDownload.Add(s3File);
                    }
                    else
                    {
                        result.SkippedCount++;
                    }
                }
                else
                {
                    // File doesn't exist locally - download
                    toDownload.Add(s3File);
                }
            }
            
            // Check for local files not in S3 (to delete)
            foreach (var localFile in localFiles)
            {
                if (!s3FileDict.ContainsKey(localFile.Key))
                {
                    toDelete.Add(localFile.Value.FullName);
                }
            }
            
            // Download files in parallel
            if (toDownload.Count > 0)
            {
                syncProgress.Status = "Downloading files...";
                syncProgress.TotalFiles = toDownload.Count;
                progress?.Report(syncProgress);
                
                var downloadChannel = Channel.CreateUnbounded<S3FileItem>();
                
                // Producer
                var producerTask = Task.Run(async () =>
                {
                    foreach (var file in toDownload)
                    {
                        await downloadChannel.Writer.WriteAsync(file, cancellationToken);
                    }
                    downloadChannel.Writer.Complete();
                }, cancellationToken);
                
                // Consumers
                var consumerTasks = Enumerable.Range(0, _performanceConfig.MaxConcurrentDownloads)
                    .Select(async _ =>
                    {
                        await foreach (var s3File in downloadChannel.Reader.ReadAllAsync(cancellationToken))
                        {
                            try
                            {
                                var relativePath = s3File.Key;
                                if (!string.IsNullOrEmpty(s3Prefix))
                                {
                                    relativePath = s3File.Key.Substring(s3Prefix.Length).TrimStart('/');
                                }
                                
                                var localFilePath = Path.Combine(localPath, relativePath.Replace('/', '\\'));
                                var localDir = Path.GetDirectoryName(localFilePath) ?? localPath;
                                
                                await DownloadFileAsync(s3File.Key, localDir, null, cancellationToken);
                                
                                result.DownloadedCount++;
                                syncProgress.ProcessedFiles = result.DownloadedCount;
                                progress?.Report(syncProgress);
                            }
                            catch (Exception ex)
                            {
                                result.Errors.Add($"Failed to download {s3File.Key}: {ex.Message}");
                            }
                        }
                    });
                
                await producerTask;
                await Task.WhenAll(consumerTasks);
            }
            
            // Handle deletions
            result.DeletedFiles = toDelete;
            
            syncProgress.Status = "Sync completed";
            syncProgress.ProcessedFiles = syncProgress.TotalFiles;
            progress?.Report(syncProgress);
            
            return result;
        }
        
        /// <summary>
        /// Delete file from S3
        /// </summary>
        public async Task DeleteFileAsync(string s3Key, CancellationToken cancellationToken = default)
        {
            ValidateCredentials("delete file from S3");
            var request = new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = s3Key
            };
            
            await _s3Client.DeleteObjectAsync(request, cancellationToken);
            await _metadataService.RemoveFileAccessRolesAsync(s3Key);
            
            // Remove from cache
            _metadataCache.TryRemove(s3Key, out _);
        }
        
        /// <summary>
        /// Check if user can access file based on role
        /// </summary>
        private bool CanUserAccessFile(UserRole userRole, S3FileItem item)
        {
            switch (userRole)
            {
                case UserRole.Administrator:
                    return true;
                case UserRole.Executive:
                    return item.AccessRoles.Contains(UserRole.Executive) ||
                           item.AccessRoles.Contains(UserRole.Administrator) ||
                           item.Key.StartsWith("executive-committee/");
                case UserRole.User:
                    return item.AccessRoles.Contains(UserRole.User);
                default:
                    return false;
            }
        }
        
        /// <summary>
        /// Get cached metadata
        /// </summary>
        private S3ObjectMetadata? GetCachedMetadata(string key)
        {
            if (!_performanceConfig.EnableMetadataCache)
                return null;
            
            if (_metadataCache.TryGetValue(key, out var metadata))
            {
                if (metadata.CacheExpiry > DateTime.UtcNow)
                    return metadata;
                
                _metadataCache.TryRemove(key, out _);
            }
            
            return null;
        }
        
        /// <summary>
        /// Update cached metadata
        /// </summary>
        private void UpdateCachedMetadata(string key, DateTime lastModified, long size)
        {
            if (!_performanceConfig.EnableMetadataCache)
                return;
            
            var metadata = new S3ObjectMetadata
            {
                Key = key,
                LastModified = lastModified,
                Size = size,
                CacheExpiry = DateTime.UtcNow.AddMinutes(_performanceConfig.MetadataCacheDurationMinutes)
            };
            
            _metadataCache.AddOrUpdate(key, metadata, (k, v) => metadata);
        }
        
        /// <summary>
        /// Cleanup expired cache entries
        /// </summary>
        private void CleanupCache(object? state)
        {
            var now = DateTime.UtcNow;
            var keysToRemove = _metadataCache
                .Where(kvp => kvp.Value.CacheExpiry < now)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var key in keysToRemove)
            {
                _metadataCache.TryRemove(key, out _);
            }
        }
        
        /// <summary>
        /// Get content type from file extension
        /// </summary>
        private string GetContentType(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLower();
            return extension switch
            {
                ".txt" => "text/plain",
                ".html" or ".htm" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".json" => "application/json",
                ".pdf" => "application/pdf",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".zip" => "application/zip",
                ".doc" or ".docx" => "application/msword",
                ".xls" or ".xlsx" => "application/vnd.ms-excel",
                _ => "application/octet-stream"
            };
        }
        
        public void Dispose()
        {
            _cacheCleanupTimer?.Dispose();
            _uploadSemaphore?.Dispose();
            _downloadSemaphore?.Dispose();
            _transferUtility?.Dispose();
            _s3Client?.Dispose();
        }
    }
    
    /// <summary>
    /// S3 object metadata for caching
    /// </summary>
    public class S3ObjectMetadata
    {
        public string Key { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
        public long Size { get; set; }
        public DateTime CacheExpiry { get; set; }
    }
    
    /// <summary>
    /// Sync operation result
    /// </summary>
    public class SyncResult
    {
        public int DownloadedCount { get; set; }
        public int SkippedCount { get; set; }
        public List<string> DeletedFiles { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();
    }
    
    /// <summary>
    /// Sync progress information
    /// </summary>
    public class SyncProgress
    {
        public string Status { get; set; } = string.Empty;
        public int TotalFiles { get; set; }
        public int ProcessedFiles { get; set; }
        public double PercentComplete => TotalFiles > 0 ? (double)ProcessedFiles / TotalFiles * 100 : 0;
    }
}
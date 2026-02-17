using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using FileSyncApp.Core.Interfaces;
using FileSyncApp.Core.Models;
using Microsoft.Extensions.Logging;

namespace FileSyncApp.S3.Services;

public class S3FileStorageService : IFileStorageService, IDisposable
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly ILogger<S3FileStorageService> _logger;
    private readonly S3MetadataService _metadataService;
    private readonly TransferUtility _transferUtility;
    private readonly SemaphoreSlim _transferSemaphore;
    private readonly long _maxBytesPerSecond;

    public S3FileStorageService(
        IAmazonS3 s3Client,
        IConfigurationService configService,
        ILogger<S3FileStorageService> logger,
        ILogger<S3MetadataService> metadataLogger)
    {
        _s3Client = s3Client;
        var config = configService.GetConfiguration();
        _bucketName = config.AWS.BucketName;
        _logger = logger;
        _metadataService = new S3MetadataService(s3Client, _bucketName, metadataLogger);

        _transferUtility = new TransferUtility(_s3Client);
        _transferSemaphore = new SemaphoreSlim(config.Performance.MaxConcurrentUploads);
        _maxBytesPerSecond = config.Performance.MaxBytesPerSecond;
    }

    public async Task<List<FileNode>> ListFilesAsync(UserRole userRole, string prefix = "", CancellationToken cancellationToken = default)
    {
        var files = new List<FileNode>();
        string? continuationToken = null;

        do
        {
            var request = new ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = prefix,
                ContinuationToken = continuationToken
            };

            var response = await _s3Client.ListObjectsV2Async(request, cancellationToken);

            foreach (var obj in response.S3Objects)
            {
                var accessRoles = await _metadataService.GetFileAccessRolesAsync(obj.Key);

                var node = new FileNode(
                    Path.GetFileName(obj.Key) ?? obj.Key,
                    obj.Key,
                    obj.Key.EndsWith("/"),
                    obj.Size ?? 0,
                    obj.LastModified ?? DateTime.MinValue,
                    accessRoles);

                if (CanUserAccessFile(userRole, node))
                {
                    files.Add(node);
                }
            }

            continuationToken = response.NextContinuationToken;
        } while (!string.IsNullOrEmpty(continuationToken) && !cancellationToken.IsCancellationRequested);

        return files;
    }

    public async Task<bool> UploadFileAsync(string filePath, string key, List<UserRole> accessRoles, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        await _transferSemaphore.WaitAsync(cancellationToken);
        try
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var throttledStream = new FileSyncApp.Core.Services.ThrottledStream(fileStream, _maxBytesPerSecond);

            var uploadRequest = new TransferUtilityUploadRequest
            {
                BucketName = _bucketName,
                Key = key,
                InputStream = throttledStream
            };

            if (progress != null)
            {
                uploadRequest.UploadProgressEvent += (s, e) => progress.Report((double)e.TransferredBytes / e.TotalBytes * 100);
            }

            await _transferUtility.UploadAsync(uploadRequest, cancellationToken);
            await _metadataService.SetFileAccessRolesAsync(key, accessRoles);
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
            var fullPath = Path.Combine(localRootPath, s3Key.Replace("/", "\\"));
            var directory = Path.GetDirectoryName(fullPath);
            if (directory != null) Directory.CreateDirectory(directory);

            var getRequest = new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = s3Key
            };

            using var response = await _s3Client.GetObjectAsync(getRequest, cancellationToken);
            using var responseStream = response.ResponseStream;
            using var throttledStream = new FileSyncApp.Core.Services.ThrottledStream(responseStream, _maxBytesPerSecond);

            using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await throttledStream.CopyToAsync(fileStream, 81920, cancellationToken);

            File.SetLastWriteTimeUtc(fullPath, response.LastModified.GetValueOrDefault().ToUniversalTime());
        }
        finally
        {
            _transferSemaphore.Release();
        }
    }

    public async Task DeleteFileAsync(string s3Key, CancellationToken cancellationToken = default)
    {
        await _s3Client.DeleteObjectAsync(_bucketName, s3Key, cancellationToken);
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
        _transferUtility.Dispose();
        _transferSemaphore.Dispose();
    }
}

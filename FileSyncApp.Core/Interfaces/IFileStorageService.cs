using FileSyncApp.Core.Models;

namespace FileSyncApp.Core.Interfaces;

public interface IFileStorageService
{
    Task<List<FileNode>> ListFilesAsync(UserRole userRole, string prefix = "", CancellationToken cancellationToken = default);
    Task<bool> UploadFileAsync(string filePath, string key, List<UserRole> accessRoles, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
    Task DownloadFileAsync(string s3Key, string localPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
    Task DeleteFileAsync(string s3Key, CancellationToken cancellationToken = default);
    Task DownloadAsZipAsync(IEnumerable<string> s3Keys, string zipFilePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
    Task DownloadFolderAsync(string s3Prefix, string localBasePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
    Task DeleteFilesAsync(IEnumerable<string> s3Keys, CancellationToken cancellationToken = default);
    Task<string?> GetPresignedUrlAsync(string s3Key, TimeSpan expiry, CancellationToken cancellationToken = default);
}

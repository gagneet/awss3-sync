using FileSyncApp.Core.Models;

namespace FileSyncApp.Core.Interfaces;

public interface IFileStorageService
{
    Task<List<FileNode>> ListFilesAsync(UserRole userRole, string prefix = "", CancellationToken cancellationToken = default);
    Task<bool> UploadFileAsync(string filePath, string key, List<UserRole> accessRoles, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
    Task DownloadFileAsync(string s3Key, string localPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
    Task DeleteFileAsync(string s3Key, CancellationToken cancellationToken = default);
}

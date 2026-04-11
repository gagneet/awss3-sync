using System.Diagnostics;
using FileSyncApp.Core.Interfaces;
using FileSyncApp.Core.Models;
using Microsoft.Extensions.Logging;

namespace FileSyncApp.Core.Services;

/// <summary>
/// Core sync engine implementing bidirectional synchronization with conflict resolution
/// </summary>
public class SyncEngine : ISyncEngine
{
    private readonly IFileStorageService _remoteStorage;
    private readonly IAuthService _authService;
    private readonly MetadataCache _metadataCache;
    private readonly ILogger<SyncEngine> _logger;
    private readonly ConflictPolicy _defaultPolicy;

    public event EventHandler<ConflictEventArgs>? ConflictsDetected;

    public SyncEngine(
        IFileStorageService remoteStorage,
        IAuthService authService,
        MetadataCache metadataCache,
        ILogger<SyncEngine> logger,
        ConflictPolicy defaultPolicy = ConflictPolicy.NewerWins)
    {
        _remoteStorage = remoteStorage;
        _authService = authService;
        _metadataCache = metadataCache;
        _logger = logger;
        _defaultPolicy = defaultPolicy;
    }

    public async Task<SyncPlan> CalculateChangesAsync(string localPath, string remotePrefix, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Calculating sync changes for {LocalPath} <-> {RemotePrefix}", localPath, remotePrefix);

        var plan = new SyncPlan { LocalRootPath = localPath };
        var user = _authService.GetCurrentUser();
        if (user == null)
        {
            _logger.LogWarning("No authenticated user, cannot calculate sync");
            return plan;
        }

        // Get last sync snapshot
        var snapshot = await _metadataCache.GetAllRecordsAsync(localPath);
        var snapshotDict = snapshot.ToDictionary(r => r.RelativePath, r => r);

        // Scan local files
        var localFiles = await ScanLocalFilesAsync(localPath, cancellationToken);
        _logger.LogInformation("Found {Count} local files", localFiles.Count);

        // List all remote files recursively so nested files are compared correctly
        var remoteFiles = await _remoteStorage.ListAllFilesRecursiveAsync(user.Role, remotePrefix, cancellationToken);
        var remoteDict = remoteFiles.Where(f => !f.IsDirectory).ToDictionary(f => f.Path, f => f);
        _logger.LogInformation("Found {Count} remote files", remoteFiles.Count);

        // Process each local file
        foreach (var localFile in localFiles)
        {
            var relativePath = GetRelativePath(localPath, localFile.FullPath);
            var remotePath = CombinePaths(remotePrefix, relativePath);

            var snapshotRecord = snapshotDict.GetValueOrDefault(relativePath);
            var remoteFile = remoteDict.GetValueOrDefault(remotePath);

            var action = DetermineAction(localFile, remoteFile, snapshotRecord);

            switch (action)
            {
                case LocalSyncAction.Upload:
                    plan.Uploads.Add(new SyncOperation
                    {
                        Type = SyncOperationType.Upload,
                        LocalPath = localFile.FullPath,
                        RemotePath = remotePath,
                        Size = localFile.Size,
                        LocalModified = localFile.LastModified,
                        Direction = SyncDirection.LocalToRemote
                    });
                    break;

                case LocalSyncAction.Download:
                    plan.Downloads.Add(new SyncOperation
                    {
                        Type = SyncOperationType.Download,
                        LocalPath = localFile.FullPath,
                        RemotePath = remotePath,
                        Size = remoteFile!.Size,
                        RemoteModified = remoteFile.LastModified,
                        Direction = SyncDirection.RemoteToLocal
                    });
                    break;

                case LocalSyncAction.Conflict:
                    plan.Conflicts.Add(new ConflictInfo
                    {
                        LocalPath = localFile.FullPath,
                        RemotePath = remotePath,
                        LocalSize = localFile.Size,
                        RemoteSize = remoteFile!.Size,
                        LocalModified = localFile.LastModified,
                        RemoteModified = remoteFile.LastModified,
                        LocalHash = localFile.Hash ?? string.Empty,
                        RemoteHash = remoteFile.ETag ?? string.Empty
                    });
                    break;

                case LocalSyncAction.DeleteRemote:
                    plan.RemoteDeletes.Add(new SyncOperation
                    {
                        Type = SyncOperationType.Delete,
                        RemotePath = remotePath,
                        Direction = SyncDirection.LocalToRemote
                    });
                    break;
            }

            // Remove processed remote files
            remoteDict.Remove(remotePath);
        }

        // Process remaining remote files (new on remote or deleted locally)
        foreach (var (remotePath, remoteFile) in remoteDict)
        {
            var relativePath = GetRelativePathFromRemote(remotePrefix, remotePath);
            var localPath2 = Path.Combine(localPath, relativePath);
            var snapshotRecord = snapshotDict.GetValueOrDefault(relativePath);

            // Skip if the computed local path is an existing directory (S3 folder-placeholder)
            if (Directory.Exists(localPath2))
            {
                _logger.LogDebug("Skipping remote object '{Remote}': local path is a directory", remotePath);
                continue;
            }

            if (snapshotRecord == null)
            {
                // New file on remote - download
                plan.Downloads.Add(new SyncOperation
                {
                    Type = SyncOperationType.Download,
                    LocalPath = localPath2,
                    RemotePath = remotePath,
                    Size = remoteFile.Size,
                    RemoteModified = remoteFile.LastModified,
                    Direction = SyncDirection.RemoteToLocal
                });
            }
            else
            {
                // Was in snapshot, now only on remote = deleted locally
                // If remote hasn't changed since snapshot, delete from remote too
                if (remoteFile.LastModified <= snapshotRecord.LastModified.AddSeconds(1))
                {
                    plan.RemoteDeletes.Add(new SyncOperation
                    {
                        Type = SyncOperationType.Delete,
                        RemotePath = remotePath,
                        Direction = SyncDirection.LocalToRemote
                    });
                }
                else
                {
                    // Remote was modified after local delete - conflict
                    plan.Conflicts.Add(new ConflictInfo
                    {
                        LocalPath = localPath2,
                        RemotePath = remotePath,
                        RemoteSize = remoteFile.Size,
                        RemoteModified = remoteFile.LastModified
                    });
                }
            }
        }

        _logger.LogInformation(
            "Sync plan: {Uploads} uploads, {Downloads} downloads, {Deletes} deletes, {Conflicts} conflicts",
            plan.Uploads.Count, plan.Downloads.Count, 
            plan.LocalDeletes.Count + plan.RemoteDeletes.Count, plan.Conflicts.Count);

        return plan;
    }

    public async Task<SyncResult> ExecuteSyncAsync(SyncPlan plan, IProgress<SyncProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new SyncResult();
        var totalOps = plan.TotalOperations;
        var completedOps = 0;
        long bytesTransferred = 0;

        var user = _authService.GetCurrentUser();
        if (user == null)
        {
            result.Errors++;
            result.ErrorDetails.Add(new SyncError { Message = "No authenticated user" });
            return result;
        }

        try
        {
            // Handle conflicts first
            if (plan.HasConflicts)
            {
                progress?.Report(new SyncProgress("Resolving conflicts...", 0, totalOps, 0, 0, SyncPhase.ResolvingConflicts));
                await ResolveConflictsAsync(plan, cancellationToken);
            }

            // Execute uploads
            progress?.Report(new SyncProgress("Uploading files...", completedOps, totalOps, bytesTransferred, 
                plan.TotalUploadBytes + plan.TotalDownloadBytes, SyncPhase.Uploading));

            foreach (var op in plan.Uploads)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    var localFilePath = op.LocalPath;
                    var uploadSize = op.Size;
                    var transferProgress = new Progress<double>(pct =>
                    {
                        progress?.Report(new SyncProgress(
                            Path.GetFileName(localFilePath), completedOps, totalOps,
                            bytesTransferred + (long)(pct / 100.0 * uploadSize),
                            plan.TotalUploadBytes + plan.TotalDownloadBytes,
                            SyncPhase.Uploading));
                    });

                    await _remoteStorage.UploadFileAsync(
                        op.LocalPath, op.RemotePath,
                        new List<UserRole> { user.Role },
                        transferProgress, cancellationToken);

                    result.FilesUploaded++;
                    bytesTransferred += op.Size;

                    // Update snapshot using the plan's local root so relative paths are correct
                    if (!string.IsNullOrEmpty(plan.LocalRootPath) && File.Exists(op.LocalPath))
                    {
                        var relPath = GetRelativePath(plan.LocalRootPath, op.LocalPath);
                        await _metadataCache.UpdateRecordAsync(
                            plan.LocalRootPath, relPath,
                            op.Size, File.GetLastWriteTimeUtc(op.LocalPath), string.Empty);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to upload {Path}", op.LocalPath);
                    result.Errors++;
                    result.ErrorDetails.Add(new SyncError
                    {
                        FilePath = op.LocalPath,
                        Message = ex.Message,
                        Exception = ex,
                        IsRetryable = true
                    });
                }

                completedOps++;
            }

            // Execute downloads
            progress?.Report(new SyncProgress("Downloading files...", completedOps, totalOps, bytesTransferred,
                plan.TotalUploadBytes + plan.TotalDownloadBytes, SyncPhase.Downloading));

            foreach (var op in plan.Downloads)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    var remoteFilePath = op.RemotePath;
                    var downloadSize = op.Size;
                    var transferProgress = new Progress<double>(pct =>
                    {
                        progress?.Report(new SyncProgress(
                            Path.GetFileName(remoteFilePath), completedOps, totalOps,
                            bytesTransferred + (long)(pct / 100.0 * downloadSize),
                            plan.TotalUploadBytes + plan.TotalDownloadBytes,
                            SyncPhase.Downloading));
                    });

                    // Ensure directory exists
                    var dir = Path.GetDirectoryName(op.LocalPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    await _remoteStorage.DownloadFileAsync(
                        op.RemotePath, op.LocalPath,
                        transferProgress, cancellationToken);

                    result.FilesDownloaded++;
                    bytesTransferred += op.Size;

                    // Update snapshot using the plan's local root
                    if (!string.IsNullOrEmpty(plan.LocalRootPath) && File.Exists(op.LocalPath))
                    {
                        var relPath = GetRelativePath(plan.LocalRootPath, op.LocalPath);
                        await _metadataCache.UpdateRecordAsync(
                            plan.LocalRootPath, relPath,
                            op.Size, File.GetLastWriteTimeUtc(op.LocalPath), string.Empty);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to download {Path}", op.RemotePath);
                    result.Errors++;
                    result.ErrorDetails.Add(new SyncError
                    {
                        FilePath = op.RemotePath,
                        Message = ex.Message,
                        Exception = ex,
                        IsRetryable = true
                    });
                }

                completedOps++;
            }

            // Execute deletes
            progress?.Report(new SyncProgress("Cleaning up...", completedOps, totalOps, bytesTransferred,
                plan.TotalUploadBytes + plan.TotalDownloadBytes, SyncPhase.Deleting));

            foreach (var op in plan.RemoteDeletes)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    await _remoteStorage.DeleteFileAsync(op.RemotePath, cancellationToken);
                    result.FilesDeleted++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete remote {Path}", op.RemotePath);
                    result.Errors++;
                }

                completedOps++;
            }

            foreach (var op in plan.LocalDeletes)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    if (File.Exists(op.LocalPath))
                    {
                        File.Delete(op.LocalPath);
                        result.FilesDeleted++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete local {Path}", op.LocalPath);
                    result.Errors++;
                }

                completedOps++;
            }

            progress?.Report(new SyncProgress("Complete", totalOps, totalOps, bytesTransferred,
                plan.TotalUploadBytes + plan.TotalDownloadBytes, SyncPhase.Complete));

            result.Success = result.Errors == 0;
            result.BytesTransferred = bytesTransferred;
        }
        finally
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
        }

        return result;
    }

    public async Task<SyncResult> SyncAsync(string localPath, string remotePrefix,
        ConflictPolicy conflictPolicy = ConflictPolicy.PromptUser,
        IProgress<SyncProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(new SyncProgress("Scanning files...", 0, 0, 0, 0, SyncPhase.Scanning));

        var plan = await CalculateChangesAsync(localPath, remotePrefix, cancellationToken);

        if (plan.HasConflicts && conflictPolicy == ConflictPolicy.PromptUser)
        {
            // Raise event for UI to handle
            var args = new ConflictEventArgs(plan.Conflicts);
            ConflictsDetected?.Invoke(this, args);

            if (!args.Handled)
            {
                // Apply default resolution
                ApplyDefaultConflictPolicy(plan.Conflicts, _defaultPolicy);
            }
        }
        else if (plan.HasConflicts)
        {
            ApplyDefaultConflictPolicy(plan.Conflicts, conflictPolicy);
        }

        // Move resolved conflicts to appropriate lists
        foreach (var conflict in plan.Conflicts.Where(c => c.Resolution.HasValue))
        {
            switch (conflict.Resolution!.Value)
            {
                case ConflictResolution.KeepLocal:
                    plan.Uploads.Add(new SyncOperation
                    {
                        Type = SyncOperationType.Upload,
                        LocalPath = conflict.LocalPath,
                        RemotePath = conflict.RemotePath,
                        Size = conflict.LocalSize,
                        Direction = SyncDirection.LocalToRemote
                    });
                    break;

                case ConflictResolution.KeepRemote:
                    plan.Downloads.Add(new SyncOperation
                    {
                        Type = SyncOperationType.Download,
                        LocalPath = conflict.LocalPath,
                        RemotePath = conflict.RemotePath,
                        Size = conflict.RemoteSize,
                        Direction = SyncDirection.RemoteToLocal
                    });
                    break;

                case ConflictResolution.KeepBoth:
                    // Rename local file with conflict suffix
                    var newLocalPath = GetConflictRenamedPath(conflict.LocalPath);
                    if (File.Exists(conflict.LocalPath))
                    {
                        File.Move(conflict.LocalPath, newLocalPath);
                    }
                    // Download remote version to original path
                    plan.Downloads.Add(new SyncOperation
                    {
                        Type = SyncOperationType.Download,
                        LocalPath = conflict.LocalPath,
                        RemotePath = conflict.RemotePath,
                        Size = conflict.RemoteSize,
                        Direction = SyncDirection.RemoteToLocal
                    });
                    // Upload renamed local as new file
                    var renamedRemotePath = GetConflictRenamedPath(conflict.RemotePath);
                    plan.Uploads.Add(new SyncOperation
                    {
                        Type = SyncOperationType.Upload,
                        LocalPath = newLocalPath,
                        RemotePath = renamedRemotePath,
                        Size = conflict.LocalSize,
                        Direction = SyncDirection.LocalToRemote
                    });
                    break;
            }
        }

        var result = await ExecuteSyncAsync(plan, progress, cancellationToken);
        await RemoveOrphanedSnapshotsAsync(localPath, remotePrefix, cancellationToken);
        return result;
    }

    public async Task<DateTime?> GetLastSyncTimeAsync(string localPath)
    {
        return await _metadataCache.GetLastSyncTimeAsync(localPath);
    }

    /// <summary>
    /// Determine the sync action for a file based on its local, remote, and last-known snapshot state.
    /// Exposed for testability.
    /// </summary>
    public SyncActionType ResolveBidirectional(FileNode? local, FileNode? remote, SnapshotEntry? snapshot)
    {
        bool localExists = local != null;
        bool remoteExists = remote != null;
        bool snapshotExists = snapshot != null;

        bool localChanged = !snapshotExists ||
            (localExists && (local!.LastModified > snapshot!.LastModified.AddSeconds(1) || local.Size != snapshot.Size));
        bool remoteChanged = !snapshotExists ||
            (remoteExists && (remote!.LastModified > snapshot!.LastModified.AddSeconds(1) || remote.Size != snapshot.Size));

        if (!localExists && snapshotExists && remoteExists && !remoteChanged)
            return SyncActionType.DeleteRemote;

        if (localExists && snapshotExists && !remoteExists && !localChanged)
            return SyncActionType.DeleteLocal;

        if (localExists && !remoteExists && !snapshotExists)
            return SyncActionType.Upload;

        if (!localExists && remoteExists && !snapshotExists)
            return SyncActionType.Download;

        if (localExists && remoteExists && localChanged && remoteChanged)
            return SyncActionType.Conflict;

        if (localExists && localChanged && !remoteChanged)
            return SyncActionType.Upload;

        if (localExists && !localChanged && remoteChanged)
            return SyncActionType.Download;

        return SyncActionType.Skip;
    }

    private async Task RemoveOrphanedSnapshotsAsync(string localPath, string remotePrefix, CancellationToken cancellationToken)
    {
        var user = _authService.GetCurrentUser();
        if (user == null) return;

        var records = await _metadataCache.GetAllRecordsAsync(localPath);
        if (records.Count == 0) return;

        var remoteFiles = await _remoteStorage.ListAllFilesRecursiveAsync(user.Role, remotePrefix, cancellationToken);
        var remoteRelPaths = new HashSet<string>(
            remoteFiles.Where(f => !f.IsDirectory)
                       .Select(f => GetRelativePathFromRemote(remotePrefix, f.Path)));

        foreach (var record in records)
        {
            var fullPath = Path.Combine(localPath, record.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath) && !remoteRelPaths.Contains(record.RelativePath))
            {
                await _metadataCache.DeleteRecordAsync(localPath, record.RelativePath);
                _logger.LogDebug("Removed stale snapshot entry: {RelPath}", record.RelativePath);
            }
        }
    }

    private LocalSyncAction DetermineAction(LocalFileInfo local, FileNode? remote, FileRecord? snapshot)
    {
        bool localExists = true;
        bool remoteExists = remote != null;
        bool snapshotExists = snapshot != null;

        bool localChanged = !snapshotExists || 
            local.LastModified > snapshot!.LastModified.AddSeconds(1) ||
            local.Size != snapshot.Size;

        bool remoteChanged = !snapshotExists || (remoteExists && 
            (remote!.LastModified > snapshot!.LastModified.AddSeconds(1) ||
             remote.Size != snapshot.Size));

        // New local file
        if (!remoteExists && !snapshotExists)
            return LocalSyncAction.Upload;

        // Local deleted, was in snapshot
        if (!localExists && snapshotExists && remoteExists && !remoteChanged)
            return LocalSyncAction.DeleteRemote;

        // Only local changed
        if (localChanged && !remoteChanged)
            return LocalSyncAction.Upload;

        // Only remote changed
        if (!localChanged && remoteChanged)
            return LocalSyncAction.Download;

        // Both changed - conflict
        if (localChanged && remoteChanged)
            return LocalSyncAction.Conflict;

        // No changes
        return LocalSyncAction.None;
    }

    private void ApplyDefaultConflictPolicy(List<ConflictInfo> conflicts, ConflictPolicy policy)
    {
        foreach (var conflict in conflicts)
        {
            conflict.Resolution = policy switch
            {
                ConflictPolicy.NewerWins => conflict.LocalModified > conflict.RemoteModified
                    ? ConflictResolution.KeepLocal
                    : ConflictResolution.KeepRemote,
                ConflictPolicy.LocalWins => ConflictResolution.KeepLocal,
                ConflictPolicy.RemoteWins => ConflictResolution.KeepRemote,
                ConflictPolicy.KeepBoth => ConflictResolution.KeepBoth,
                _ => ConflictResolution.Skip
            };
        }
    }

    private async Task ResolveConflictsAsync(SyncPlan plan, CancellationToken cancellationToken)
    {
        // Conflicts should already have resolutions from UI or default policy
        // This method can add additional logic if needed
        await Task.CompletedTask;
    }

    private async Task<List<LocalFileInfo>> ScanLocalFilesAsync(string basePath, CancellationToken cancellationToken)
    {
        var files = new List<LocalFileInfo>();

        if (!Directory.Exists(basePath))
            return files;

        await Task.Run(() =>
        {
            foreach (var filePath in Directory.EnumerateFiles(basePath, "*", SearchOption.AllDirectories))
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    var info = new FileInfo(filePath);
                    files.Add(new LocalFileInfo
                    {
                        FullPath = filePath,
                        Size = info.Length,
                        LastModified = info.LastWriteTimeUtc
                    });
                }
                catch (UnauthorizedAccessException)
                {
                    // Skip files we can't access
                }
            }
        }, cancellationToken);

        return files;
    }

    private string GetRelativePath(string basePath, string fullPath)
    {
        return Path.GetRelativePath(basePath, fullPath).Replace('\\', '/');
    }

    private string GetRelativePathFromRemote(string prefix, string remotePath)
    {
        if (string.IsNullOrEmpty(prefix))
            return remotePath;

        return remotePath.StartsWith(prefix) 
            ? remotePath.Substring(prefix.Length).TrimStart('/') 
            : remotePath;
    }

    private string CombinePaths(string prefix, string relativePath)
    {
        if (string.IsNullOrEmpty(prefix))
            return relativePath;

        return prefix.TrimEnd('/') + "/" + relativePath;
    }

    private string GetConflictRenamedPath(string path)
    {
        var dir = Path.GetDirectoryName(path) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd-HHmmss");

        return Path.Combine(dir, $"{name} (conflict {timestamp}){ext}");
    }

    private enum LocalSyncAction
    {
        None,
        Upload,
        Download,
        Conflict,
        DeleteLocal,
        DeleteRemote
    }

    private class LocalFileInfo
    {
        public string FullPath { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public string? Hash { get; set; }
    }
}

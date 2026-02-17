using FileSyncApp.Core.Interfaces;
using FileSyncApp.Core.Models;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("FileSyncApp.Tests")]

namespace FileSyncApp.Core.Services;

public class SyncEngine : ISyncEngine
{
    private readonly IFileStorageService _remoteStorage;
    private readonly IDatabaseService _dbService;

    public SyncEngine(IFileStorageService remoteStorage, IDatabaseService dbService)
    {
        _remoteStorage = remoteStorage;
        _dbService = dbService;
    }

    public async Task SyncAsync(string localPath, string remotePrefix, UserRole userRole, IProgress<SyncProgress> progress, Func<SyncActionRequest, Task<SyncActionType>>? conflictResolver, CancellationToken ct)
    {
        progress.Report(new SyncProgress("Scanning local files...", 0, 0));
        var localFiles = ScanLocalDirectory(localPath).ToDictionary(f => f.Path);

        progress.Report(new SyncProgress("Scanning remote files...", 0, 0));
        var remoteFiles = (await _remoteStorage.ListFilesAsync(userRole, remotePrefix, ct)).ToDictionary(f => f.Path);

        var snapshots = _dbService.GetSnapshots().ToDictionary(s => s.Path);

        var allPaths = localFiles.Keys.Union(remoteFiles.Keys).Union(snapshots.Keys).ToList();
        var actions = new List<SyncActionRequest>();

        foreach (var path in allPaths)
        {
            localFiles.TryGetValue(path, out var local);
            remoteFiles.TryGetValue(path, out var remote);
            snapshots.TryGetValue(path, out var snapshot);

            var action = ResolveBidirectional(local, remote, snapshot);
            if (action != SyncActionType.Skip)
            {
                actions.Add(new SyncActionRequest(path, action, local, remote));
            }
        }

        int total = actions.Count;
        int processed = 0;

        foreach (var req in actions)
        {
            if (ct.IsCancellationRequested) break;
            progress.Report(new SyncProgress($"Syncing {req.Path}...", total, processed));

            try
            {
                var actionToExecute = req.Action;
                if (actionToExecute == SyncActionType.Conflict && conflictResolver != null)
                {
                    actionToExecute = await conflictResolver(req);
                }

                if (actionToExecute != SyncActionType.Skip && actionToExecute != SyncActionType.Conflict)
                {
                    var updatedReq = req with { Action = actionToExecute };
                    await ExecuteAction(updatedReq, localPath, userRole, ct);

                    if (actionToExecute == SyncActionType.DeleteLocal || actionToExecute == SyncActionType.DeleteRemote)
                    {
                        _dbService.DeleteSnapshot(req.Path);
                    }
                    else
                    {
                        var updatedLocal = GetFileNode(Path.Combine(localPath, req.Path.Replace("/", "\\")), req.Path);
                        if (updatedLocal != null)
                        {
                            _dbService.SaveSnapshot(updatedLocal);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Log and continue
            }

            processed++;
        }

        progress.Report(new SyncProgress("Sync complete", total, processed));
    }

    internal SyncActionType ResolveBidirectional(FileNode? local, FileNode? remote, SnapshotEntry? snapshot)
    {
        bool localChanged = IsChanged(local, snapshot);
        bool remoteChanged = IsChanged(remote, snapshot);

        if (!localChanged && !remoteChanged) return SyncActionType.Skip;

        // If both deleted since last sync
        if (local == null && remote == null && snapshot != null) return SyncActionType.DeleteLocal; // Either DeleteLocal or DeleteRemote will trigger snapshot deletion

        if (localChanged && !remoteChanged)
        {
            return local == null ? SyncActionType.DeleteRemote : SyncActionType.Upload;
        }
        if (!localChanged && remoteChanged)
        {
            return remote == null ? SyncActionType.DeleteLocal : SyncActionType.Download;
        }

        return SyncActionType.Conflict;
    }

    private bool IsChanged(FileNode? current, SnapshotEntry? snapshot)
    {
        if (current == null && snapshot == null) return false;
        if (current == null || snapshot == null) return true;
        return current.Size != snapshot.Size || Math.Abs((current.LastModified - snapshot.LastModified).TotalSeconds) > 1;
    }

    public async Task ExecuteAction(SyncActionRequest req, string localRoot, UserRole userRole, CancellationToken ct)
    {
        var localPath = Path.Combine(localRoot, req.Path.Replace("/", "\\"));
        var localDir = Path.GetDirectoryName(localPath) ?? localRoot;

        switch (req.Action)
        {
            case SyncActionType.Upload:
                await _remoteStorage.UploadFileAsync(localPath, req.Path, new List<UserRole> { userRole }, null, ct);
                break;
            case SyncActionType.Download:
                await _remoteStorage.DownloadFileAsync(req.Path, localRoot, null, ct);
                break;
            case SyncActionType.DeleteLocal:
                if (File.Exists(localPath)) File.Delete(localPath);
                break;
            case SyncActionType.DeleteRemote:
                await _remoteStorage.DeleteFileAsync(req.Path, ct);
                break;
            case SyncActionType.KeepBoth:
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HHmmss");
                var extension = Path.GetExtension(localPath);
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(localPath);
                var newLocalPath = Path.Combine(localDir, $"{fileNameWithoutExtension} (conflict copy {timestamp}){extension}");

                if (File.Exists(localPath))
                {
                    File.Move(localPath, newLocalPath);
                }
                await _remoteStorage.DownloadFileAsync(req.Path, localRoot, null, ct);
                break;
        }
    }

    private List<FileNode> ScanLocalDirectory(string path)
    {
        var result = new List<FileNode>();
        if (!Directory.Exists(path)) return result;
        var dirInfo = new DirectoryInfo(path);
        foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(path, file.FullName).Replace("\\", "/");
            result.Add(new FileNode(file.Name, relativePath, false, file.Length, file.LastWriteTimeUtc));
        }
        return result;
    }

    private FileNode? GetFileNode(string fullPath, string relativePath)
    {
        if (!File.Exists(fullPath)) return null;
        var info = new FileInfo(fullPath);
        return new FileNode(info.Name, relativePath, false, info.Length, info.LastWriteTimeUtc);
    }
}

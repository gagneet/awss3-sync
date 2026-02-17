using FileSyncApp.Core.Models;

namespace FileSyncApp.Core.Interfaces;

public interface ISyncEngine
{
    Task SyncAsync(string localPath, string remotePrefix, UserRole userRole, IProgress<SyncProgress> progress, Func<SyncActionRequest, Task<SyncActionType>>? conflictResolver, CancellationToken ct);
}

public record SyncProgress(string Status, int TotalFiles, int ProcessedFiles)
{
    public double PercentComplete => TotalFiles > 0 ? (double)ProcessedFiles / TotalFiles * 100 : 0;
}

using FileSyncApp.Core.Models;

namespace FileSyncApp.Core.Interfaces;

/// <summary>
/// Orchestrates bidirectional sync between local and remote storage
/// </summary>
public interface ISyncEngine
{
    /// <summary>
    /// Calculate changes needed to sync local and remote
    /// </summary>
    Task<SyncPlan> CalculateChangesAsync(string localPath, string remotePrefix, CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute a sync plan
    /// </summary>
    Task<SyncResult> ExecuteSyncAsync(SyncPlan plan, IProgress<SyncProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Perform full bidirectional sync
    /// </summary>
    Task<SyncResult> SyncAsync(string localPath, string remotePrefix, 
        ConflictPolicy conflictPolicy = ConflictPolicy.PromptUser,
        IProgress<SyncProgress>? progress = null, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get last sync timestamp for a path
    /// </summary>
    Task<DateTime?> GetLastSyncTimeAsync(string localPath);

    /// <summary>
    /// Event raised when conflicts are detected
    /// </summary>
    event EventHandler<ConflictEventArgs>? ConflictsDetected;
}

/// <summary>
/// Planned sync operations before execution
/// </summary>
public class SyncPlan
{
    /// <summary>The local root directory used when this plan was calculated.</summary>
    public string LocalRootPath { get; set; } = string.Empty;

    public List<SyncOperation> Uploads { get; set; } = new();
    public List<SyncOperation> Downloads { get; set; } = new();
    public List<SyncOperation> LocalDeletes { get; set; } = new();
    public List<SyncOperation> RemoteDeletes { get; set; } = new();
    public List<ConflictInfo> Conflicts { get; set; } = new();

    public int TotalOperations => Uploads.Count + Downloads.Count + LocalDeletes.Count + RemoteDeletes.Count;
    public long TotalUploadBytes => Uploads.Sum(o => o.Size);
    public long TotalDownloadBytes => Downloads.Sum(o => o.Size);
    public bool HasConflicts => Conflicts.Count > 0;
}

/// <summary>
/// Result of a sync operation
/// </summary>
public class SyncResult
{
    public bool Success { get; set; }
    public int FilesUploaded { get; set; }
    public int FilesDownloaded { get; set; }
    public int FilesDeleted { get; set; }
    public int ConflictsResolved { get; set; }
    public int Errors { get; set; }
    public List<SyncError> ErrorDetails { get; set; } = new();
    public TimeSpan Duration { get; set; }
    public long BytesTransferred { get; set; }
}

/// <summary>
/// Individual sync operation
/// </summary>
public class SyncOperation
{
    public SyncOperationType Type { get; set; }
    public string LocalPath { get; set; } = string.Empty;
    public string RemotePath { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime LocalModified { get; set; }
    public DateTime RemoteModified { get; set; }
    public SyncDirection Direction { get; set; }
}

public enum SyncOperationType
{
    Upload,
    Download,
    Delete,
    Conflict
}

public enum SyncDirection
{
    LocalToRemote,
    RemoteToLocal,
    Bidirectional
}

/// <summary>
/// Conflict information
/// </summary>
public class ConflictInfo
{
    public string LocalPath { get; set; } = string.Empty;
    public string RemotePath { get; set; } = string.Empty;
    public long LocalSize { get; set; }
    public long RemoteSize { get; set; }
    public DateTime LocalModified { get; set; }
    public DateTime RemoteModified { get; set; }
    public string LocalHash { get; set; } = string.Empty;
    public string RemoteHash { get; set; } = string.Empty;
    public ConflictResolution? Resolution { get; set; }
}

public enum ConflictPolicy
{
    NewerWins,
    LocalWins,
    RemoteWins,
    KeepBoth,
    PromptUser
}

public enum ConflictResolution
{
    KeepLocal,
    KeepRemote,
    KeepBoth,
    Skip
}

/// <summary>
/// Sync progress information
/// </summary>
public record SyncProgress(
    string CurrentFile,
    int CompletedOperations,
    int TotalOperations,
    long BytesTransferred,
    long TotalBytes,
    SyncPhase Phase
);

public enum SyncPhase
{
    Scanning,
    Comparing,
    ResolvingConflicts,
    Uploading,
    Downloading,
    Deleting,
    Finalizing,
    Complete
}

/// <summary>
/// Error during sync
/// </summary>
public class SyncError
{
    public string FilePath { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
    public bool IsRetryable { get; set; }
}

/// <summary>
/// Event args for conflict detection
/// </summary>
public class ConflictEventArgs : EventArgs
{
    public List<ConflictInfo> Conflicts { get; }
    public bool Handled { get; set; }

    public ConflictEventArgs(List<ConflictInfo> conflicts)
    {
        Conflicts = conflicts;
    }
}

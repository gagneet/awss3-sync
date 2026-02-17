using FileSyncApp.Core.Models;

namespace FileSyncApp.Core.Interfaces;

public interface IDatabaseService
{
    void SaveSnapshot(FileNode node);
    void DeleteSnapshot(string path);
    List<SnapshotEntry> GetSnapshots();
}

public record SnapshotEntry(string Path, long Size, DateTime LastModified, string S3Key, string VersionId);

using FileSyncApp.Core.Models;

namespace FileSyncApp.Core.Interfaces;

public interface IFileSyncView
{
    string StatusMessage { set; }
    int ProgressValue { set; }
    bool ProgressVisible { set; }

    event EventHandler SyncRequested;
    event EventHandler CancelRequested;
    event EventHandler RefreshRequested;

    void UpdateLocalTree(List<FileNode> nodes);
    void UpdateRemoteTree(List<FileNode> nodes);
}

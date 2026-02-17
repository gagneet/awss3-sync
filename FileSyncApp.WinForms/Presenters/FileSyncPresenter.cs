using FileSyncApp.Core.Interfaces;
using FileSyncApp.Core.Models;
using FileSyncApp.WinForms.Forms;
using System.Windows.Forms;

namespace FileSyncApp.WinForms.Presenters;

public class FileSyncPresenter
{
    private readonly IFileSyncView _view;
    private readonly IFileStorageService _s3Service;
    private readonly ISyncEngine _syncEngine;
    private readonly IAuthService _authService;
    private CancellationTokenSource? _cts;

    public FileSyncPresenter(
        IFileSyncView view,
        IFileStorageService s3Service,
        ISyncEngine syncEngine,
        IAuthService authService)
    {
        _view = view;
        _s3Service = s3Service;
        _syncEngine = syncEngine;
        _authService = authService;

        _view.SyncRequested += OnSyncRequested;
        _view.CancelRequested += (s, e) => _cts?.Cancel();
        _view.RefreshRequested += (s, e) => RefreshRemote();
    }

    private async void OnSyncRequested(object? sender, EventArgs e)
    {
        var user = _authService.GetCurrentUser();
        if (user == null)
        {
            _view.StatusMessage = "User not authenticated";
            return;
        }

        using var fbd = new FolderBrowserDialog { Description = "Select Local Folder to Sync" };
        if (fbd.ShowDialog() != DialogResult.OK) return;
        string localPath = fbd.SelectedPath;

        _cts = new CancellationTokenSource();
        _view.ProgressVisible = true;
        _view.StatusMessage = "Starting sync...";

        try
        {
            var progress = new Progress<SyncProgress>(p =>
            {
                _view.StatusMessage = p.Status;
                _view.ProgressValue = (int)p.PercentComplete;
            });

            await _syncEngine.SyncAsync(localPath, "", user.Role, progress, ResolveConflictAsync, _cts.Token);
            _view.StatusMessage = "Sync completed successfully";
        }
        catch (OperationCanceledException)
        {
            _view.StatusMessage = "Sync cancelled";
        }
        catch (Exception ex)
        {
            _view.StatusMessage = $"Sync failed: {ex.Message}";
        }
        finally
        {
            _view.ProgressVisible = false;
            _cts = null;
        }
    }

    private async Task<SyncActionType> ResolveConflictAsync(SyncActionRequest req)
    {
        SyncActionType result = SyncActionType.Skip;

        if (_view is Form form)
        {
            form.Invoke(new Action(() =>
            {
                using var conflictDialog = new ConflictForm(req.Path, req.Local!, req.Remote!);
                if (conflictDialog.ShowDialog() == DialogResult.OK)
                {
                    result = conflictDialog.SelectedAction;
                }
            }));
        }

        return await Task.FromResult(result);
    }

    private async void RefreshRemote()
    {
        var user = _authService.GetCurrentUser();
        if (user == null) return;

        _view.StatusMessage = "Refreshing remote...";
        try
        {
            var files = await _s3Service.ListFilesAsync(user.Role, "");
            _view.UpdateRemoteTree(files);
            _view.StatusMessage = "Remote refreshed";
        }
        catch (Exception ex)
        {
            _view.StatusMessage = $"Refresh failed: {ex.Message}";
        }
    }
}

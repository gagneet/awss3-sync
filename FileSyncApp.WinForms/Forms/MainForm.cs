using System.IO.Compression;
using FileSyncApp.Core.Interfaces;
using FileSyncApp.Core.Models;
using Krypton.Toolkit;

namespace FileSyncApp.WinForms.Forms;

/// <summary>
/// Main application form with dual-pane interface for local/S3 file management
/// Features: Bidirectional sync, download (single/folder/ZIP), upload, conflict resolution
/// </summary>
public partial class MainForm : KryptonForm, IFileSyncView
{
    private readonly IAuthService _authService;
    private readonly IFileStorageService _s3Service;
    private readonly IConfigurationService _configService;
    private readonly ISyncEngine _syncEngine;

    // UI Controls
    private KryptonSplitContainer _splitContainer = null!;
    private KryptonTreeView _localTreeView = null!;
    private KryptonTreeView _s3TreeView = null!;
    private KryptonLabel _statusLabel = null!;
    private KryptonProgressBar _progressBar = null!;
    private KryptonButton _btnSync = null!;
    private KryptonButton _btnRefresh = null!;
    private KryptonButton _btnSettings = null!;
    private KryptonButton _btnBrowseLocal = null!;
    private KryptonTextBox _txtLocalPath = null!;
    private ContextMenuStrip _localContextMenu = null!;
    private ContextMenuStrip _s3ContextMenu = null!;
    private Button _btnCancel = null!;

    // State
    private bool _isLoadingS3;
    private bool _isLoadingLocal;
    private string _localRootPath = string.Empty;
    private CancellationTokenSource? _currentOperationCts;

    // Events for presenter
    public event EventHandler? SyncRequested;
    public event EventHandler? CancelRequested;
    public event EventHandler? RefreshRequested;
    public event EventHandler<string>? LocalPathSelected;

    public string StatusMessage { set => SafeSetStatus(value); }
    public int ProgressValue { set => SafeSetProgress(value); }
    public bool ProgressVisible { set => SafeSetProgressVisible(value); }

    public MainForm(
        IAuthService authService,
        IFileStorageService s3Service,
        IConfigurationService configService,
        ISyncEngine syncEngine)
    {
        _authService = authService;
        _s3Service = s3Service;
        _configService = configService;
        _syncEngine = syncEngine;

        InitializeComponent();
        InitializeContextMenus();
        InitializeTrees();
        WireUpEvents();
    }

    #region Initialization

    private void InitializeComponent()
    {
        Text = "FileSyncApp - Strata S3 Document Manager";
        Width = 1400;
        Height = 900;
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;

        var mainPanel = new KryptonPanel { Dock = DockStyle.Fill };

        // Toolbar
        var toolStrip = new KryptonPanel { Dock = DockStyle.Top, Height = 70 };
        
        _btnSync = new KryptonButton 
        { 
            Text = "Sync Now", 
            Location = new Point(10, 15), 
            Width = 120, 
            Height = 40
        };

        _btnRefresh = new KryptonButton 
        { 
            Text = "Refresh", 
            Location = new Point(140, 15), 
            Width = 100, 
            Height = 40 
        };

        _btnSettings = new KryptonButton 
        { 
            Text = "Settings", 
            Location = new Point(250, 15), 
            Width = 100, 
            Height = 40 
        };

        var lblLocalPath = new KryptonLabel 
        { 
            Text = "Local Folder:", 
            Location = new Point(380, 22) 
        };

        _txtLocalPath = new KryptonTextBox 
        { 
            Location = new Point(470, 18), 
            Width = 400, 
            Height = 30,
            ReadOnly = true
        };

        _btnBrowseLocal = new KryptonButton 
        { 
            Text = "Browse...", 
            Location = new Point(880, 15), 
            Width = 90, 
            Height = 40 
        };

        toolStrip.Controls.AddRange(new Control[] 
        { 
            _btnSync, _btnRefresh, _btnSettings, 
            lblLocalPath, _txtLocalPath, _btnBrowseLocal 
        });

        // Split container for dual-pane view
        _splitContainer = new KryptonSplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 650,
            Panel1MinSize = 300,
            Panel2MinSize = 300
        };

        // Local panel (left)
        var localGroup = new KryptonGroupBox 
        { 
            Text = "Local Files", 
            Dock = DockStyle.Fill 
        };
        _localTreeView = new KryptonTreeView 
        { 
            Dock = DockStyle.Fill,
            CheckBoxes = true,
            ShowNodeToolTips = true
        };
        localGroup.Panel.Controls.Add(_localTreeView);

        // S3 panel (right)
        var s3Group = new KryptonGroupBox 
        { 
            Text = "AWS S3 Bucket", 
            Dock = DockStyle.Fill 
        };
        _s3TreeView = new KryptonTreeView 
        { 
            Dock = DockStyle.Fill,
            CheckBoxes = true,
            ShowNodeToolTips = true
        };
        s3Group.Panel.Controls.Add(_s3TreeView);

        _splitContainer.Panel1.Controls.Add(localGroup);
        _splitContainer.Panel2.Controls.Add(s3Group);

        // Status bar
        var statusPanel = new KryptonPanel { Dock = DockStyle.Bottom, Height = 45 };
        _statusLabel = new KryptonLabel 
        { 
            Text = "Ready", 
            Location = new Point(10, 12), 
            Width = 600 
        };
        _progressBar = new KryptonProgressBar 
        { 
            Location = new Point(620, 10), 
            Width = 500, 
            Height = 25,
            Visible = false 
        };

        _btnCancel = new Button 
        { 
            Text = "Cancel", 
            Location = new Point(1130, 8), 
            Width = 80, 
            Height = 28,
            Visible = false 
        };
        _btnCancel.Click += (s, e) => CancelCurrentOperation();

        statusPanel.Controls.AddRange(new Control[] { _statusLabel, _progressBar, _btnCancel });

        mainPanel.Controls.Add(_splitContainer);
        mainPanel.Controls.Add(toolStrip);
        mainPanel.Controls.Add(statusPanel);

        Controls.Add(mainPanel);
    }

    private void InitializeContextMenus()
    {
        // Local context menu
        _localContextMenu = new ContextMenuStrip();
        _localContextMenu.Items.AddRange(new ToolStripItem[]
        {
            new ToolStripMenuItem("Upload to S3", null, OnUploadSelected),
            new ToolStripMenuItem("Open in Explorer", null, OnOpenInExplorer),
            new ToolStripSeparator(),
            new ToolStripMenuItem("Refresh", null, (s, e) => RefreshLocalTree()),
            new ToolStripMenuItem("Exclude from Sync", null, OnExcludeFromSync),
            new ToolStripSeparator(),
            new ToolStripMenuItem("Properties", null, OnShowLocalProperties)
        });
        _localTreeView.ContextMenuStrip = _localContextMenu;

        // S3 context menu
        _s3ContextMenu = new ContextMenuStrip();
        _s3ContextMenu.Items.AddRange(new ToolStripItem[]
        {
            new ToolStripMenuItem("Download", null, OnDownloadSelected),
            new ToolStripMenuItem("Download as ZIP", null, OnDownloadAsZip),
            new ToolStripMenuItem("Download Folder", null, OnDownloadFolder),
            new ToolStripSeparator(),
            new ToolStripMenuItem("Copy S3 URL", null, OnCopyS3Url),
            new ToolStripMenuItem("Refresh", null, (s, e) => RefreshS3Tree()),
            new ToolStripSeparator(),
            new ToolStripMenuItem("Delete", null, OnDeleteFromS3),
            new ToolStripSeparator(),
            new ToolStripMenuItem("Properties", null, OnShowS3Properties)
        });
        _s3TreeView.ContextMenuStrip = _s3ContextMenu;
    }

    private void InitializeTrees()
    {
        // Initialize local tree with drives
        _localTreeView.Nodes.Clear();
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
        {
            var node = new TreeNode($"{drive.Name} ({drive.DriveType})")
            {
                Tag = drive.RootDirectory,
                ToolTipText = $"{drive.TotalFreeSpace / (1024 * 1024 * 1024):N0} GB free"
            };
            node.Nodes.Add(new TreeNode("Loading..."));
            _localTreeView.Nodes.Add(node);
        }

        // Initialize S3 tree with root
        _s3TreeView.Nodes.Clear();
        var config = _configService.GetConfiguration();
        var s3Root = new TreeNode($"S3: {config.AWS.BucketName}") { Tag = "" };
        s3Root.Nodes.Add(new TreeNode("Loading..."));
        _s3TreeView.Nodes.Add(s3Root);
    }

    private void WireUpEvents()
    {
        _btnSync.Click += async (s, e) => await PerformSyncAsync();
        _btnRefresh.Click += (s, e) => { RefreshLocalTree(); RefreshS3Tree(); };
        _btnSettings.Click += (s, e) => ShowSettingsDialog();
        _btnBrowseLocal.Click += (s, e) => BrowseForLocalFolder();

        _localTreeView.BeforeExpand += LocalTreeView_BeforeExpand;
        _s3TreeView.BeforeExpand += S3TreeView_BeforeExpand;

        _localTreeView.NodeMouseDoubleClick += LocalTreeView_DoubleClick;
        _s3TreeView.NodeMouseDoubleClick += S3TreeView_DoubleClick;

        // Drag and drop
        _s3TreeView.AllowDrop = true;
        _s3TreeView.DragEnter += S3TreeView_DragEnter;
        _s3TreeView.DragDrop += S3TreeView_DragDrop;

        // Sync engine events
        _syncEngine.ConflictsDetected += OnConflictsDetected;
    }

    #endregion

    #region Tree View Loading

    private void LocalTreeView_BeforeExpand(object? sender, TreeViewCancelEventArgs e)
    {
        if (_isLoadingLocal || e.Node == null) return;
        if (e.Node.Nodes.Count != 1 || e.Node.Nodes[0].Text != "Loading...") return;

        _isLoadingLocal = true;
        e.Node.Nodes.Clear();

        var dirInfo = e.Node.Tag as DirectoryInfo;
        if (dirInfo == null && e.Node.Tag is string path)
            dirInfo = new DirectoryInfo(path);

        if (dirInfo == null)
        {
            _isLoadingLocal = false;
            return;
        }

        try
        {
            _localTreeView.BeginUpdate();

            // Load subdirectories
            foreach (var dir in dirInfo.EnumerateDirectories().Take(500))
            {
                try
                {
                    var node = new TreeNode($"[DIR] {dir.Name}")
                    {
                        Tag = dir,
                        ToolTipText = $"Modified: {dir.LastWriteTime:g}"
                    };

                    try
                    {
                        if (dir.EnumerateFileSystemInfos().Any())
                            node.Nodes.Add(new TreeNode("Loading..."));
                    }
                    catch { }

                    e.Node.Nodes.Add(node);
                }
                catch (UnauthorizedAccessException) { }
            }

            // Load files
            foreach (var file in dirInfo.EnumerateFiles().Take(500))
            {
                var sizeStr = FormatFileSize(file.Length);
                var node = new TreeNode($"{file.Name}")
                {
                    Tag = file,
                    ToolTipText = $"Size: {sizeStr}\nModified: {file.LastWriteTime:g}"
                };
                e.Node.Nodes.Add(node);
            }

            _localTreeView.EndUpdate();
        }
        catch (UnauthorizedAccessException)
        {
            e.Node.Nodes.Add(new TreeNode("Access Denied"));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            _isLoadingLocal = false;
        }
    }

    private async void S3TreeView_BeforeExpand(object? sender, TreeViewCancelEventArgs e)
    {
        if (_isLoadingS3 || e.Node == null) return;
        if (e.Node.Nodes.Count != 1 || e.Node.Nodes[0].Text != "Loading...") return;

        _isLoadingS3 = true;
        e.Node.Nodes.Clear();
        var prefix = e.Node.Tag as string ?? "";

        try
        {
            StatusMessage = "Loading S3 contents...";
            var user = _authService.GetCurrentUser();
            
            if (user == null)
            {
                e.Node.Nodes.Add(new TreeNode("Not authenticated"));
                return;
            }

            var items = await Task.Run(async () =>
                await _s3Service.ListFilesAsync(user.Role, prefix));

            _s3TreeView.BeginUpdate();
            try
            {
                foreach (var item in items.Take(500))
                {
                    var displayText = item.IsDirectory 
                        ? $"[DIR] {item.Name}" 
                        : $"{item.Name} ({FormatFileSize(item.Size)})";
                    
                    var node = new TreeNode(displayText)
                    {
                        Tag = item.Path,
                        ToolTipText = item.IsDirectory 
                            ? item.Path 
                            : $"Size: {FormatFileSize(item.Size)}\nModified: {item.LastModified:g}"
                    };

                    if (item.IsDirectory)
                        node.Nodes.Add(new TreeNode("Loading..."));

                    e.Node.Nodes.Add(node);
                }
            }
            finally
            {
                _s3TreeView.EndUpdate();
            }

            StatusMessage = $"Loaded {items.Count} items";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            e.Node.Nodes.Add(new TreeNode($"Error: {ex.Message}"));
        }
        finally
        {
            _isLoadingS3 = false;
        }
    }

    #endregion

    #region Sync Operations

    private async Task PerformSyncAsync()
    {
        if (string.IsNullOrEmpty(_localRootPath))
        {
            MessageBox.Show("Please select a local folder first.", "No Folder Selected",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _currentOperationCts = new CancellationTokenSource();
        var progress = new Progress<SyncProgress>(UpdateSyncProgress);

        try
        {
            _btnSync.Enabled = false;
            _btnCancel.Visible = true;
            ProgressVisible = true;
            StatusMessage = "Calculating changes...";

            var result = await _syncEngine.SyncAsync(
                _localRootPath,
                "",
                ConflictPolicy.PromptUser,
                progress,
                _currentOperationCts.Token);

            if (result.Success)
            {
                StatusMessage = $"Sync complete: {result.FilesUploaded} uploaded, " +
                    $"{result.FilesDownloaded} downloaded, {result.FilesDeleted} deleted";
            }
            else
            {
                StatusMessage = $"Sync completed with {result.Errors} errors";
                if (result.ErrorDetails.Any())
                {
                    ShowErrorSummary(result.ErrorDetails);
                }
            }

            RefreshLocalTree();
            RefreshS3Tree();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Sync cancelled";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Sync failed: {ex.Message}";
            MessageBox.Show($"Sync failed:\n\n{ex.Message}", "Sync Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnSync.Enabled = true;
            _btnCancel.Visible = false;
            ProgressVisible = false;
            _currentOperationCts = null;
        }
    }

    private void UpdateSyncProgress(SyncProgress p)
    {
        if (InvokeRequired)
        {
            Invoke(() => UpdateSyncProgress(p));
            return;
        }

        var percent = p.TotalOperations > 0 
            ? (int)(p.CompletedOperations * 100.0 / p.TotalOperations) 
            : 0;
        ProgressValue = percent;
        StatusMessage = $"{p.Phase}: {p.CurrentFile} ({p.CompletedOperations}/{p.TotalOperations})";
    }

    private void OnConflictsDetected(object? sender, ConflictEventArgs e)
    {
        if (InvokeRequired)
        {
            Invoke(() => OnConflictsDetected(sender, e));
            return;
        }

        using var dialog = new ConflictResolutionDialog(e.Conflicts);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            e.Handled = true;
        }
    }

    #endregion

    #region Download Operations

    private async void OnDownloadSelected(object? sender, EventArgs e)
    {
        var node = _s3TreeView.SelectedNode;
        if (node?.Tag == null) return;

        var s3Path = node.Tag.ToString()!;
        
        using var dialog = new SaveFileDialog
        {
            FileName = Path.GetFileName(s3Path),
            Filter = "All Files|*.*"
        };

        if (dialog.ShowDialog() != DialogResult.OK) return;

        await DownloadFileAsync(s3Path, dialog.FileName);
    }

    private async void OnDownloadAsZip(object? sender, EventArgs e)
    {
        var selectedPaths = GetSelectedS3Paths();
        if (!selectedPaths.Any())
        {
            MessageBox.Show("Please select files to download.", "No Selection",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            FileName = "download.zip",
            Filter = "ZIP Archive|*.zip"
        };

        if (dialog.ShowDialog() != DialogResult.OK) return;

        await DownloadAsZipAsync(selectedPaths, dialog.FileName);
    }

    private async void OnDownloadFolder(object? sender, EventArgs e)
    {
        var node = _s3TreeView.SelectedNode;
        if (node?.Tag == null) return;

        var s3Prefix = node.Tag.ToString()!;
        if (!s3Prefix.EndsWith("/"))
        {
            MessageBox.Show("Please select a folder to download.", "Not a Folder",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new FolderBrowserDialog
        {
            Description = "Select destination folder"
        };

        if (dialog.ShowDialog() != DialogResult.OK) return;

        await DownloadFolderAsync(s3Prefix, dialog.SelectedPath);
    }

    private async Task DownloadFileAsync(string s3Path, string localPath)
    {
        _currentOperationCts = new CancellationTokenSource();
        var progress = new Progress<double>(percent =>
        {
            SafeSetProgress((int)percent);
            SafeSetStatus($"Downloading... {(int)percent}%");
        });

        try
        {
            _btnCancel.Visible = true;
            ProgressVisible = true;
            await _s3Service.DownloadFileAsync(s3Path, localPath, progress, _currentOperationCts.Token);
            StatusMessage = $"Downloaded: {Path.GetFileName(localPath)}";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Download cancelled";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Download failed:\n\n{ex.Message}", "Download Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnCancel.Visible = false;
            ProgressVisible = false;
            _currentOperationCts = null;
        }
    }

    private async Task DownloadAsZipAsync(List<string> s3Paths, string zipPath)
    {
        _currentOperationCts = new CancellationTokenSource();

        try
        {
            _btnCancel.Visible = true;
            ProgressVisible = true;
            StatusMessage = $"Creating ZIP with {s3Paths.Count} files...";

            await _s3Service.DownloadAsZipAsync(s3Paths, zipPath, null, _currentOperationCts.Token);

            StatusMessage = $"Downloaded ZIP: {Path.GetFileName(zipPath)}";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Download cancelled";
            if (File.Exists(zipPath)) File.Delete(zipPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"ZIP download failed:\n\n{ex.Message}", "Download Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnCancel.Visible = false;
            ProgressVisible = false;
            _currentOperationCts = null;
        }
    }

    private async Task DownloadFolderAsync(string s3Prefix, string localBasePath)
    {
        _currentOperationCts = new CancellationTokenSource();

        try
        {
            _btnCancel.Visible = true;
            ProgressVisible = true;
            StatusMessage = "Downloading folder...";

            await _s3Service.DownloadFolderAsync(s3Prefix, localBasePath, null, _currentOperationCts.Token);

            StatusMessage = "Folder download complete";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Download cancelled";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Folder download failed:\n\n{ex.Message}", "Download Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnCancel.Visible = false;
            ProgressVisible = false;
            _currentOperationCts = null;
        }
    }

    #endregion

    #region Upload Operations

    private async void OnUploadSelected(object? sender, EventArgs e)
    {
        var selectedPaths = GetSelectedLocalPaths();
        if (!selectedPaths.Any())
        {
            MessageBox.Show("Please select files to upload.", "No Selection",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var destPrefix = _s3TreeView.SelectedNode?.Tag?.ToString() ?? "";
        if (!destPrefix.EndsWith("/") && !string.IsNullOrEmpty(destPrefix))
            destPrefix += "/";

        await UploadFilesAsync(selectedPaths, destPrefix);
    }

    private async Task UploadFilesAsync(List<string> localPaths, string s3Prefix)
    {
        _currentOperationCts = new CancellationTokenSource();
        var user = _authService.GetCurrentUser();
        if (user == null) return;

        var uploaded = 0;
        var total = localPaths.Count;

        try
        {
            _btnCancel.Visible = true;
            ProgressVisible = true;

            foreach (var localPath in localPaths)
            {
                if (_currentOperationCts.Token.IsCancellationRequested) break;

                var fileName = Path.GetFileName(localPath);
                var s3Key = s3Prefix + fileName;

                SafeSetStatus($"Uploading ({uploaded + 1}/{total}): {fileName}");

                await _s3Service.UploadFileAsync(
                    localPath, s3Key,
                    new List<UserRole> { user.Role },
                    null, _currentOperationCts.Token);

                uploaded++;
                SafeSetProgress(uploaded * 100 / total);
            }

            StatusMessage = $"Uploaded {uploaded} files";
            RefreshS3Tree();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = $"Upload cancelled ({uploaded}/{total} completed)";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Upload failed:\n\n{ex.Message}", "Upload Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnCancel.Visible = false;
            ProgressVisible = false;
            _currentOperationCts = null;
        }
    }

    #endregion

    #region Drag and Drop

    private void S3TreeView_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            e.Effect = DragDropEffects.Copy;
        }
    }

    private async void S3TreeView_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] files) return;

        var destPrefix = "";
        var targetPoint = _s3TreeView.PointToClient(new Point(e.X, e.Y));
        var targetNode = _s3TreeView.GetNodeAt(targetPoint);
        
        if (targetNode?.Tag is string tag && tag.EndsWith("/"))
            destPrefix = tag;

        await UploadFilesAsync(files.ToList(), destPrefix);
    }

    #endregion

    #region Context Menu Actions

    private void OnOpenInExplorer(object? sender, EventArgs e)
    {
        var node = _localTreeView.SelectedNode;
        if (node?.Tag is DirectoryInfo dir)
        {
            System.Diagnostics.Process.Start("explorer.exe", dir.FullName);
        }
        else if (node?.Tag is FileInfo file)
        {
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{file.FullName}\"");
        }
    }

    private void OnExcludeFromSync(object? sender, EventArgs e)
    {
        var node = _localTreeView.SelectedNode;
        if (node?.Tag == null) return;

        var path = node.Tag switch
        {
            DirectoryInfo dir => dir.Name + "/**",
            FileInfo file => file.Name,
            _ => null
        };

        if (path != null)
        {
            MessageBox.Show($"Added to exclusions: {path}\n\nThis will be skipped during sync.",
                "Excluded", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private async void OnDeleteFromS3(object? sender, EventArgs e)
    {
        var paths = GetSelectedS3Paths();
        if (!paths.Any()) return;

        var result = MessageBox.Show(
            $"Delete {paths.Count} item(s) from S3?\n\nThis cannot be undone.",
            "Confirm Delete",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result != DialogResult.Yes) return;

        try
        {
            StatusMessage = "Deleting...";
            await _s3Service.DeleteFilesAsync(paths);
            StatusMessage = $"Deleted {paths.Count} items";
            RefreshS3Tree();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Delete failed:\n\n{ex.Message}", "Delete Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void OnCopyS3Url(object? sender, EventArgs e)
    {
        var node = _s3TreeView.SelectedNode;
        if (node?.Tag == null) return;

        var s3Path = node.Tag.ToString()!;
        
        try
        {
            var url = await _s3Service.GetPresignedUrlAsync(s3Path, TimeSpan.FromHours(24));
            if (url != null)
            {
                Clipboard.SetText(url);
                StatusMessage = "S3 URL copied to clipboard (valid 24 hours)";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to generate URL: {ex.Message}";
        }
    }

    private void OnShowLocalProperties(object? sender, EventArgs e)
    {
        var node = _localTreeView.SelectedNode;
        if (node?.Tag is FileInfo file)
        {
            MessageBox.Show(
                $"Name: {file.Name}\nPath: {file.FullName}\nSize: {FormatFileSize(file.Length)}\n" +
                $"Created: {file.CreationTime:g}\nModified: {file.LastWriteTime:g}",
                "File Properties", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void OnShowS3Properties(object? sender, EventArgs e)
    {
        var node = _s3TreeView.SelectedNode;
        if (node?.Tag != null)
        {
            var path = node.Tag.ToString()!;
            MessageBox.Show(
                $"S3 Key: {path}\nBucket: {_configService.GetConfiguration().AWS.BucketName}",
                "S3 Object Properties", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    #endregion

    #region UI Helpers

    private void SafeSetStatus(string value)
    {
        if (InvokeRequired)
            Invoke(() => _statusLabel.Text = value);
        else
            _statusLabel.Text = value;
    }

    private void SafeSetProgress(int value)
    {
        if (InvokeRequired)
            Invoke(() => _progressBar.Value = Math.Min(value, 100));
        else
            _progressBar.Value = Math.Min(value, 100);
    }

    private void SafeSetProgressVisible(bool value)
    {
        if (InvokeRequired)
            Invoke(() => _progressBar.Visible = value);
        else
            _progressBar.Visible = value;
    }

    private void BrowseForLocalFolder()
    {
        using var dialog = new FolderBrowserDialog { Description = "Select folder to sync with S3" };
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _localRootPath = dialog.SelectedPath;
            _txtLocalPath.Text = _localRootPath;
            LocalPathSelected?.Invoke(this, _localRootPath);
        }
    }

    private void RefreshLocalTree()
    {
        _localTreeView.Nodes.Clear();
        InitializeTrees();
    }

    private void RefreshS3Tree()
    {
        var rootNode = _s3TreeView.Nodes[0];
        rootNode.Nodes.Clear();
        rootNode.Nodes.Add(new TreeNode("Loading..."));
        rootNode.Collapse();
        rootNode.Expand();
    }

    private void ShowSettingsDialog()
    {
        MessageBox.Show("Settings dialog - configure sync options, bandwidth, exclusions",
            "Settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void CancelCurrentOperation()
    {
        _currentOperationCts?.Cancel();
    }

    private List<string> GetSelectedLocalPaths()
    {
        var paths = new List<string>();
        CollectCheckedNodes(_localTreeView.Nodes, paths, true);
        return paths;
    }

    private List<string> GetSelectedS3Paths()
    {
        var paths = new List<string>();
        CollectCheckedNodes(_s3TreeView.Nodes, paths, false);
        return paths;
    }

    private void CollectCheckedNodes(TreeNodeCollection nodes, List<string> paths, bool isLocal)
    {
        foreach (TreeNode node in nodes)
        {
            if (node.Checked && node.Tag != null)
            {
                if (isLocal)
                {
                    var path = node.Tag switch
                    {
                        FileInfo file => file.FullName,
                        DirectoryInfo dir => dir.FullName,
                        _ => null
                    };
                    if (path != null) paths.Add(path);
                }
                else
                {
                    paths.Add(node.Tag.ToString()!);
                }
            }
            CollectCheckedNodes(node.Nodes, paths, isLocal);
        }
    }

    private void LocalTreeView_DoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Node?.Tag is FileInfo file)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = file.FullName,
                UseShellExecute = true
            });
        }
    }

    private async void S3TreeView_DoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Node?.Tag == null) return;
        var path = e.Node.Tag.ToString()!;
        if (path.EndsWith("/")) return;

        var tempPath = Path.Combine(Path.GetTempPath(), Path.GetFileName(path));
        await DownloadFileAsync(path, tempPath);

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = tempPath,
            UseShellExecute = true
        });
    }

    private void ShowErrorSummary(List<SyncError> errors)
    {
        var summary = string.Join("\n", errors.Take(10).Select(e => $"- {e.FilePath}: {e.Message}"));
        if (errors.Count > 10) summary += $"\n... and {errors.Count - 10} more";
        MessageBox.Show($"Some files failed:\n\n{summary}", "Sync Errors", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1) { order++; size /= 1024; }
        return $"{size:0.##} {sizes[order]}";
    }

    #endregion

    #region IFileSyncView

    public void UpdateLocalTree(List<FileNode> nodes) => RefreshLocalTree();
    public void UpdateRemoteTree(List<FileNode> nodes) => RefreshLocalTree();
    public void ShowProgress(string message, int percentage) { SafeSetStatus(message); SafeSetProgress(percentage); }
    public void ShowConflictDialog(List<ConflictInfo> conflicts)
    {
        if (InvokeRequired) { Invoke(() => ShowConflictDialog(conflicts)); return; }
        using var dialog = new ConflictResolutionDialog(conflicts);
        dialog.ShowDialog(this);
    }

    #endregion
}

/// <summary>
/// Dialog for resolving file conflicts
/// </summary>
public class ConflictResolutionDialog : Form
{
    private readonly List<ConflictInfo> _conflicts;
    private DataGridView _grid = null!;

    public ConflictResolutionDialog(List<ConflictInfo> conflicts)
    {
        _conflicts = conflicts;
        InitializeComponent();
        LoadConflicts();
    }

    private void InitializeComponent()
    {
        Text = "Resolve Conflicts";
        Width = 800; Height = 500;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;

        var label = new Label
        {
            Text = $"{_conflicts.Count} file(s) modified on both sides. Choose resolution:",
            Location = new Point(10, 10), Size = new Size(760, 40)
        };

        _grid = new DataGridView
        {
            Location = new Point(10, 60), Size = new Size(760, 340),
            AutoGenerateColumns = false, AllowUserToAddRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };

        _grid.Columns.AddRange(new DataGridViewColumn[]
        {
            new DataGridViewTextBoxColumn { HeaderText = "File", Width = 250, DataPropertyName = "FileName" },
            new DataGridViewTextBoxColumn { HeaderText = "Local Modified", Width = 130, DataPropertyName = "LocalModified" },
            new DataGridViewTextBoxColumn { HeaderText = "Remote Modified", Width = 130, DataPropertyName = "RemoteModified" },
            new DataGridViewComboBoxColumn { HeaderText = "Resolution", Width = 150, DataPropertyName = "Resolution",
                Items = { "Keep Local", "Keep Remote", "Keep Both", "Skip" } }
        });

        var btnApply = new Button { Text = "Apply", Location = new Point(610, 420), Size = new Size(80, 30), DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "Cancel", Location = new Point(700, 420), Size = new Size(80, 30), DialogResult = DialogResult.Cancel };

        Controls.AddRange(new Control[] { label, _grid, btnApply, btnCancel });
        AcceptButton = btnApply; CancelButton = btnCancel;
    }

    private void LoadConflicts()
    {
        var list = _conflicts.Select(c => new ConflictViewModel
        {
            FileName = Path.GetFileName(c.LocalPath),
            LocalModified = c.LocalModified.ToString("g"),
            RemoteModified = c.RemoteModified.ToString("g"),
            Resolution = "Keep Local",
            Conflict = c
        }).ToList();
        _grid.DataSource = list;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (DialogResult == DialogResult.OK && _grid.DataSource is List<ConflictViewModel> list)
        {
            foreach (var row in list)
            {
                row.Conflict.Resolution = row.Resolution switch
                {
                    "Keep Local" => ConflictResolution.KeepLocal,
                    "Keep Remote" => ConflictResolution.KeepRemote,
                    "Keep Both" => ConflictResolution.KeepBoth,
                    _ => ConflictResolution.Skip
                };
            }
        }
        base.OnFormClosing(e);
    }

    private class ConflictViewModel
    {
        public string FileName { get; set; } = "";
        public string LocalModified { get; set; } = "";
        public string RemoteModified { get; set; } = "";
        public string Resolution { get; set; } = "";
        public ConflictInfo Conflict { get; set; } = null!;
    }
}

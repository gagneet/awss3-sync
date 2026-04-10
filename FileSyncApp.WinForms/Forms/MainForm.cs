using System.Diagnostics;
using System.IO.Compression;
using FileSyncApp.Core.Interfaces;
using FileSyncApp.Core.Models;
using Krypton.Toolkit;

namespace FileSyncApp.WinForms.Forms;

/// <summary>
/// Dual-pane file manager: Local (left) | AWS S3 (right).
/// Navigate folders by double-clicking. Check files to select for upload/download.
/// </summary>
public partial class MainForm : KryptonForm, IFileSyncView
{
    // ── Services ──────────────────────────────────────────────────────────
    private readonly IAuthService _authService;
    private readonly IFileStorageService _s3Service;
    private readonly IConfigurationService _configService;
    private readonly ISyncEngine _syncEngine;

    // ── Toolbar buttons ───────────────────────────────────────────────────
    private KryptonButton _btnSync     = null!;
    private KryptonButton _btnUpload   = null!;
    private KryptonButton _btnDownload = null!;
    private KryptonButton _btnRefresh  = null!;
    private KryptonButton _btnSettings = null!;
    private Button        _btnCancel   = null!;

    // ── Local pane ────────────────────────────────────────────────────────
    private KryptonTextBox _txtLocalPath   = null!;
    private KryptonButton  _btnBrowseLocal = null!;
    private KryptonButton  _btnLocalUp     = null!;
    private ListView       _localListView  = null!;

    // ── S3 pane ───────────────────────────────────────────────────────────
    private KryptonTextBox _txtS3Prefix  = null!;
    private KryptonButton  _btnS3Up      = null!;
    private KryptonButton  _btnS3Refresh = null!;
    private ListView       _s3ListView   = null!;

    // ── Status bar ────────────────────────────────────────────────────────
    private KryptonLabel       _statusLabel = null!;
    private KryptonProgressBar _progressBar = null!;

    // ── Layout ────────────────────────────────────────────────────────────
    private SplitContainer _split = null!;

    // ── State ─────────────────────────────────────────────────────────────
    private string                   _localCurrentPath  = string.Empty;
    private string                   _s3CurrentPrefix   = string.Empty;
    private bool                     _isLoadingS3;
    private CancellationTokenSource? _currentOperationCts;

    // ── IFileSyncView events ──────────────────────────────────────────────
    public event EventHandler? SyncRequested;
    public event EventHandler? CancelRequested;
    public event EventHandler? RefreshRequested;
    public event EventHandler<string>? LocalPathSelected;

    public string StatusMessage  { set => SafeSetStatus(value); }
    public int    ProgressValue  { set => SafeSetProgress(value); }
    public bool   ProgressVisible { set => SafeSetProgressVisible(value); }

    // ─────────────────────────────────────────────────────────────────────
    public MainForm(
        IAuthService authService,
        IFileStorageService s3Service,
        IConfigurationService configService,
        ISyncEngine syncEngine)
    {
        _authService   = authService;
        _s3Service     = s3Service;
        _configService = configService;
        _syncEngine    = syncEngine;

        BuildUI();
        BuildContextMenus();
        WireEvents();
    }

    // ══════════════════════════════════════════════════════════════════════
    #region UI construction

    private void BuildUI()
    {
        Text          = "FileSyncApp – S3 Document Manager";
        Size          = new Size(1400, 860);
        MinimumSize   = new Size(800, 500);
        StartPosition = FormStartPosition.CenterScreen;

        // ── Toolbar ─────────────────────────────────────────────────────
        var toolbar = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 56,
            BackColor = Color.FromArgb(45, 45, 48)
        };

        _btnSync     = ToolBtn("⇄  Sync Now",       Color.FromArgb(0, 122, 204));
        _btnUpload   = ToolBtn("⬆  Upload →",       Color.FromArgb(30, 120, 0));
        _btnDownload = ToolBtn("← ⬇  Download",    Color.FromArgb(0, 100, 170));
        _btnRefresh  = ToolBtn("↺  Refresh",        Color.FromArgb(70, 70, 80));
        _btnSettings = ToolBtn("⚙  Settings",       Color.FromArgb(70, 70, 80));

        _btnCancel = new Button
        {
            Text      = "✕  Cancel",
            BackColor = Color.FromArgb(160, 0, 0),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 9, FontStyle.Bold),
            Size      = new Size(100, 38),
            Visible   = false
        };
        _btnCancel.FlatAppearance.BorderSize = 0;

        _btnUpload.Enabled   = false;
        _btnDownload.Enabled = false;

        int bx = 10;
        foreach (Control btn in new Control[] { _btnSync, _btnUpload, _btnDownload, _btnRefresh, _btnSettings, _btnCancel })
        {
            btn.Location = new Point(bx, 9);
            btn.Size     = new Size(110, 38);
            bx += 116;
        }
        toolbar.Controls.AddRange(new Control[] { _btnSync, _btnUpload, _btnDownload, _btnRefresh, _btnSettings, _btnCancel });

        // ── Split container ─────────────────────────────────────────────
        // Keep min sizes small so the [Panel1MinSize, Width-Panel2MinSize]
        // constraint is never violated during resize transitions.
        // SplitterDistance is set to 50 % via BeginInvoke in OnLoad, which
        // runs after all WM_SIZE messages have been processed.
        _split = new SplitContainer
        {
            Dock          = DockStyle.Fill,
            Orientation   = Orientation.Vertical,
            SplitterWidth = 5,
            Panel1MinSize = 100,
            Panel2MinSize = 100
        };

        _split.Panel1.Controls.Add(BuildLocalPane());
        _split.Panel2.Controls.Add(BuildS3Pane());

        // ── Status bar ──────────────────────────────────────────────────
        var statusBar = new Panel
        {
            Dock      = DockStyle.Bottom,
            Height    = 36,
            BackColor = Color.FromArgb(37, 37, 38)
        };
        _statusLabel = new KryptonLabel
        {
            Text     = "Ready — browse a local folder, then connect to S3",
            Location = new Point(8, 8),
            Width    = 700
        };
        _statusLabel.StateCommon.ShortText.Color1 = Color.White;
        _progressBar = new KryptonProgressBar
        {
            Location = new Point(720, 6),
            Size     = new Size(500, 22),
            Visible  = false,
            Maximum  = 100
        };
        statusBar.Controls.AddRange(new Control[] { _statusLabel, _progressBar });

        Controls.Add(_split);
        Controls.Add(toolbar);
        Controls.Add(statusBar);
    }

    private Panel BuildLocalPane()
    {
        var pane = new Panel { Dock = DockStyle.Fill };

        var header = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 54,
            BackColor = Color.FromArgb(28, 28, 28)
        };

        var lbl = new Label
        {
            Text      = "📁  Local Files",
            ForeColor = Color.White,
            Font      = new Font("Segoe UI", 10, FontStyle.Bold),
            Location  = new Point(8, 15),
            AutoSize  = true
        };

        _txtLocalPath = new KryptonTextBox
        {
            Location = new Point(140, 13),
            Size     = new Size(420, 28),
            ReadOnly = true
        };
        _txtLocalPath.Font = new Font("Consolas", 9);

        _btnBrowseLocal = new KryptonButton
        {
            Text     = "Browse…",
            Location = new Point(570, 11),
            Size     = new Size(80, 30)
        };
        _btnLocalUp = new KryptonButton
        {
            Text     = "⬆ Up",
            Location = new Point(658, 11),
            Size     = new Size(60, 30),
            Enabled  = false
        };

        header.Controls.AddRange(new Control[] { lbl, _txtLocalPath, _btnBrowseLocal, _btnLocalUp });

        _localListView = new ListView
        {
            Dock             = DockStyle.Fill,
            View             = View.Details,
            CheckBoxes       = true,
            FullRowSelect    = true,
            GridLines        = true,
            ShowItemToolTips = true,
            Font             = new Font("Segoe UI", 9),
            BackColor        = Color.FromArgb(30, 30, 30),
            ForeColor        = Color.White
        };
        _localListView.Columns.AddRange(new[]
        {
            new ColumnHeader { Text = "Name",     Width = 300 },
            new ColumnHeader { Text = "Size",     Width = 85, TextAlign = HorizontalAlignment.Right },
            new ColumnHeader { Text = "Modified", Width = 130 },
            new ColumnHeader { Text = "Type",     Width = 70 }
        });

        pane.Controls.Add(_localListView);
        pane.Controls.Add(header);
        return pane;
    }

    private Panel BuildS3Pane()
    {
        var pane = new Panel { Dock = DockStyle.Fill };

        var header = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 54,
            BackColor = Color.FromArgb(16, 36, 56)
        };

        var lbl = new Label
        {
            Text      = "☁  AWS S3 Bucket",
            ForeColor = Color.White,
            Font      = new Font("Segoe UI", 10, FontStyle.Bold),
            Location  = new Point(8, 15),
            AutoSize  = true
        };

        _txtS3Prefix = new KryptonTextBox
        {
            Location = new Point(172, 13),
            Size     = new Size(390, 28),
            ReadOnly = true
        };
        _txtS3Prefix.Font = new Font("Consolas", 9);

        _btnS3Up = new KryptonButton
        {
            Text     = "⬆ Up",
            Location = new Point(570, 11),
            Size     = new Size(60, 30),
            Enabled  = false
        };
        _btnS3Refresh = new KryptonButton
        {
            Text     = "↺",
            Location = new Point(638, 11),
            Size     = new Size(40, 30)
        };

        header.Controls.AddRange(new Control[] { lbl, _txtS3Prefix, _btnS3Up, _btnS3Refresh });

        _s3ListView = new ListView
        {
            Dock             = DockStyle.Fill,
            View             = View.Details,
            CheckBoxes       = true,
            FullRowSelect    = true,
            GridLines        = true,
            ShowItemToolTips = true,
            Font             = new Font("Segoe UI", 9),
            BackColor        = Color.FromArgb(20, 30, 40),
            ForeColor        = Color.White
        };
        _s3ListView.Columns.AddRange(new[]
        {
            new ColumnHeader { Text = "Name",     Width = 300 },
            new ColumnHeader { Text = "Size",     Width = 85, TextAlign = HorizontalAlignment.Right },
            new ColumnHeader { Text = "Modified", Width = 130 },
            new ColumnHeader { Text = "Key",      Width = 200 }
        });

        pane.Controls.Add(_s3ListView);
        pane.Controls.Add(header);
        return pane;
    }

    private static KryptonButton ToolBtn(string text, Color back) =>
        new KryptonButton
        {
            Text = text,
            StateCommon =
            {
                Back    = { Color1 = back, ColorStyle = PaletteColorStyle.Solid },
                Content = { ShortText = { Color1 = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold) } }
            }
        };

    private void BuildContextMenus()
    {
        var localCtx = new ContextMenuStrip();
        localCtx.Items.AddRange(new ToolStripItem[]
        {
            new ToolStripMenuItem("Upload to S3",       null, OnUploadSelected),
            new ToolStripMenuItem("Open in Explorer",   null, OnOpenInExplorer),
            new ToolStripSeparator(),
            new ToolStripMenuItem("Refresh",            null, (s, e) => LoadLocalFolder(_localCurrentPath)),
            new ToolStripSeparator(),
            new ToolStripMenuItem("Properties",         null, OnShowLocalProperties)
        });
        _localListView.ContextMenuStrip = localCtx;

        var s3Ctx = new ContextMenuStrip();
        s3Ctx.Items.AddRange(new ToolStripItem[]
        {
            new ToolStripMenuItem("Download",           null, OnDownloadSelected),
            new ToolStripMenuItem("Download as ZIP",    null, OnDownloadAsZip),
            new ToolStripMenuItem("Download Folder",    null, OnDownloadFolder),
            new ToolStripSeparator(),
            new ToolStripMenuItem("Copy Presigned URL", null, OnCopyS3Url),
            new ToolStripMenuItem("Refresh",            null, async (s, e) => await LoadS3ListAsync(_s3CurrentPrefix)),
            new ToolStripSeparator(),
            new ToolStripMenuItem("Delete from S3",     null, OnDeleteFromS3),
            new ToolStripSeparator(),
            new ToolStripMenuItem("Properties",         null, OnShowS3Properties)
        });
        _s3ListView.ContextMenuStrip = s3Ctx;
    }

    private void WireEvents()
    {
        _btnSync.Click     += async (s, e) => await PerformSyncAsync();
        _btnUpload.Click   += OnUploadSelected;
        _btnDownload.Click += OnDownloadSelected;
        _btnRefresh.Click  += async (s, e) => { LoadLocalFolder(_localCurrentPath); await LoadS3ListAsync(_s3CurrentPrefix); };
        _btnSettings.Click += (s, e) => ShowSettingsDialog();
        _btnCancel.Click   += (s, e) => CancelCurrentOperation();

        _btnBrowseLocal.Click += (s, e) => BrowseForLocalFolder();
        _btnLocalUp.Click     += (s, e) => NavigateLocalUp();
        _btnS3Up.Click        += async (s, e) => await NavigateS3UpAsync();
        _btnS3Refresh.Click   += async (s, e) => await LoadS3ListAsync(_s3CurrentPrefix);

        _localListView.ItemChecked   += (s, e) => UpdateToolbarButtons();
        _localListView.DoubleClick   += LocalList_DoubleClick;
        _s3ListView.ItemChecked      += (s, e) => UpdateToolbarButtons();
        _s3ListView.DoubleClick      += S3List_DoubleClick;

        // Drag local items onto S3 pane
        _localListView.ItemDrag += (s, e) => _localListView.DoDragDrop(e.Item!, DragDropEffects.Copy);
        _s3ListView.AllowDrop    = true;
        _s3ListView.DragEnter   += (s, e) => { if (e.Data!.GetDataPresent(typeof(ListViewItem))) e.Effect = DragDropEffects.Copy; };
        _s3ListView.DragDrop    += (s, e) => OnUploadSelected(s, EventArgs.Empty);

        _syncEngine.ConflictsDetected += OnConflictsDetected;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        // BeginInvoke posts to the message queue and runs after all pending
        // WM_SIZE / layout messages, so _split.Width is guaranteed non-zero.
        BeginInvoke(() =>
        {
            if (_split.Width > 0)
                _split.SplitterDistance = _split.Width / 2;
        });
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        await LoadS3ListAsync("");
    }

    private void UpdateToolbarButtons()
    {
        _btnUpload.Enabled   = GetCheckedLocalFiles().Any();
        _btnDownload.Enabled = GetCheckedS3Nodes().Any();
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════
    #region Local pane — navigation & loading

    private void BrowseForLocalFolder()
    {
        using var dlg = new FolderBrowserDialog { Description = "Select local folder to sync with S3" };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            LocalPathSelected?.Invoke(this, dlg.SelectedPath);
            LoadLocalFolder(dlg.SelectedPath);
        }
    }

    private void LoadLocalFolder(string path)
    {
        if (string.IsNullOrEmpty(path)) return;

        _localCurrentPath = path;
        _txtLocalPath.Text = path;
        _btnLocalUp.Enabled = Directory.GetParent(path) != null;

        _localListView.BeginUpdate();
        _localListView.Items.Clear();

        // ".." parent entry
        var parent = Directory.GetParent(path);
        if (parent != null)
        {
            var up = new ListViewItem("..")
            {
                Tag       = (object)parent,
                ForeColor = Color.DimGray,
                ToolTipText = "Go up one level"
            };
            up.SubItems.AddRange(new[] { "", "", "" });
            _localListView.Items.Add(up);
        }

        try
        {
            var dir = new DirectoryInfo(path);

            foreach (var sub in dir.EnumerateDirectories().OrderBy(d => d.Name))
            {
                try
                {
                    var item = new ListViewItem($"📁  {sub.Name}") { Tag = (object)sub };
                    item.SubItems.AddRange(new[] { "—", sub.LastWriteTime.ToString("g"), "Folder" });
                    _localListView.Items.Add(item);
                }
                catch (UnauthorizedAccessException) { }
            }

            foreach (var file in dir.EnumerateFiles().OrderBy(f => f.Name))
            {
                var item = new ListViewItem($"📄  {file.Name}") { Tag = (object)file };
                item.SubItems.AddRange(new[]
                {
                    FormatSize(file.Length),
                    file.LastWriteTime.ToString("g"),
                    file.Extension.TrimStart('.').ToUpper()
                });
                item.ToolTipText = $"Size: {FormatSize(file.Length)}  Modified: {file.LastWriteTime:g}\nPath: {file.FullName}";
                _localListView.Items.Add(item);
            }
        }
        catch (UnauthorizedAccessException)
        {
            StatusMessage = "Access denied to some folders";
        }

        _localListView.EndUpdate();
        StatusMessage = $"Local: {path}";
    }

    private void NavigateLocalUp()
    {
        var parent = Directory.GetParent(_localCurrentPath);
        if (parent != null) LoadLocalFolder(parent.FullName);
    }

    private void LocalList_DoubleClick(object? sender, EventArgs e)
    {
        if (_localListView.SelectedItems.Count == 0) return;
        var tag = _localListView.SelectedItems[0].Tag;
        if (tag is DirectoryInfo dir)
            LoadLocalFolder(dir.FullName);
        else if (tag is FileInfo file)
            Process.Start(new ProcessStartInfo { FileName = file.FullName, UseShellExecute = true });
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════
    #region S3 pane — navigation & loading

    private async Task LoadS3ListAsync(string prefix)
    {
        var user = _authService.GetCurrentUser();
        if (user == null)
        {
            StatusMessage = "Not authenticated — please restart and log in";
            return;
        }

        _isLoadingS3 = true;
        try
        {
            var config = _configService.GetConfiguration();
            _s3CurrentPrefix = prefix;
            _txtS3Prefix.Text  = $"s3://{config.AWS.BucketName}/{prefix}";
            _btnS3Up.Enabled   = !string.IsNullOrEmpty(prefix);

            StatusMessage = $"Loading S3: /{prefix} …";
            ProgressVisible = true;

            var items = await Task.Run(() => _s3Service.ListFilesAsync(user.Role, prefix));

            _s3ListView.BeginUpdate();
            _s3ListView.Items.Clear();

            // ".." entry
            if (!string.IsNullOrEmpty(prefix))
            {
                var up = new ListViewItem("..") { Tag = GetParentPrefix(prefix), ForeColor = Color.DimGray };
                up.SubItems.AddRange(new[] { "", "", "" });
                _s3ListView.Items.Add(up);
            }

            foreach (var node in items.OrderBy(n => !n.IsDirectory).ThenBy(n => n.Name))
            {
                var icon = node.IsDirectory ? "📁" : "📄";
                var lv   = new ListViewItem($"{icon}  {node.Name}") { Tag = node };
                lv.SubItems.AddRange(new[]
                {
                    node.IsDirectory ? "—"  : FormatSize(node.Size),
                    node.IsDirectory ? ""   : node.LastModified.ToString("g"),
                    node.IsDirectory ? ""   : node.Path
                });
                lv.ToolTipText = node.IsDirectory
                    ? $"Folder: {node.Path}"
                    : $"Key: {node.Path}\nSize: {FormatSize(node.Size)}\nModified: {node.LastModified:g}";
                _s3ListView.Items.Add(lv);
            }

            _s3ListView.EndUpdate();

            var files   = items.Count(n => !n.IsDirectory);
            var folders = items.Count(n => n.IsDirectory);
            StatusMessage = $"S3:  {folders} folder(s),  {files} file(s)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"S3 error: {ex.Message}";
        }
        finally
        {
            _isLoadingS3 = false;
            ProgressVisible = false;
        }
    }

    private async void S3List_DoubleClick(object? sender, EventArgs e)
    {
        if (_s3ListView.SelectedItems.Count == 0) return;
        var tag = _s3ListView.SelectedItems[0].Tag;

        if (tag is string upPrefix)
        {
            await LoadS3ListAsync(upPrefix);
        }
        else if (tag is FileNode node)
        {
            if (node.IsDirectory)
            {
                var childPrefix = node.Path.EndsWith("/") ? node.Path : node.Path + "/";
                await LoadS3ListAsync(childPrefix);
            }
            else
            {
                var tempPath = Path.Combine(Path.GetTempPath(), node.Name);
                await DownloadFileAsync(node.Path, tempPath);
                Process.Start(new ProcessStartInfo { FileName = tempPath, UseShellExecute = true });
            }
        }
    }

    private async Task NavigateS3UpAsync() => await LoadS3ListAsync(GetParentPrefix(_s3CurrentPrefix));

    private static string GetParentPrefix(string prefix)
    {
        var t = prefix.TrimEnd('/');
        var i = t.LastIndexOf('/');
        return i < 0 ? "" : t[..(i + 1)];
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════
    #region Sync

    private async Task PerformSyncAsync()
    {
        if (string.IsNullOrEmpty(_localCurrentPath))
        {
            MessageBox.Show("Browse to a local folder first.", "No Folder Selected",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _currentOperationCts = new CancellationTokenSource();
        try
        {
            _btnSync.Enabled = false;
            ProgressVisible  = true;
            StatusMessage    = "Calculating changes…";

            var plan = await _syncEngine.CalculateChangesAsync(
                _localCurrentPath, _s3CurrentPrefix, _currentOperationCts.Token);

            if (plan.TotalOperations == 0 && !plan.HasConflicts)
            {
                StatusMessage = "Everything is up to date — nothing to sync.";
                return;
            }

            using var preview = new SyncPreviewForm(plan);
            if (preview.ShowDialog(this) != DialogResult.OK) { StatusMessage = "Sync cancelled."; return; }

            var selected = preview.GetSelectedPlan();
            if (selected.TotalOperations == 0) { StatusMessage = "No operations selected."; return; }

            _btnCancel.Visible = true;
            var progress = new Progress<SyncProgress>(p =>
            {
                if (InvokeRequired) Invoke(() => ApplySyncProgress(p));
                else ApplySyncProgress(p);
            });

            var result = await _syncEngine.ExecuteSyncAsync(selected, progress, _currentOperationCts.Token);

            StatusMessage = result.Success
                ? $"Sync complete ✓  {result.FilesUploaded} uploaded,  {result.FilesDownloaded} downloaded,  {result.FilesDeleted} deleted"
                : $"Sync finished with {result.Errors} error(s)";

            if (!result.Success && result.ErrorDetails.Any()) ShowErrorSummary(result.ErrorDetails);

            LoadLocalFolder(_localCurrentPath);
            await LoadS3ListAsync(_s3CurrentPrefix);
        }
        catch (OperationCanceledException) { StatusMessage = "Sync cancelled"; }
        catch (Exception ex)
        {
            StatusMessage = $"Sync failed: {ex.Message}";
            MessageBox.Show(ex.Message, "Sync Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnSync.Enabled   = true;
            _btnCancel.Visible = false;
            ProgressVisible    = false;
            _currentOperationCts = null;
        }
    }

    private void ApplySyncProgress(SyncProgress p)
    {
        ProgressValue = p.TotalOperations > 0
            ? (int)(p.CompletedOperations * 100.0 / p.TotalOperations) : 0;
        StatusMessage = $"{p.Phase}: {p.CurrentFile}  ({p.CompletedOperations}/{p.TotalOperations})";
    }

    private void OnConflictsDetected(object? sender, ConflictEventArgs e)
    {
        if (InvokeRequired) { Invoke(() => OnConflictsDetected(sender, e)); return; }
        using var dlg = new ConflictResolutionDialog(e.Conflicts);
        if (dlg.ShowDialog(this) == DialogResult.OK) e.Handled = true;
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════
    #region Upload

    private async void OnUploadSelected(object? sender, EventArgs e)
    {
        var files = GetCheckedLocalFiles();
        if (!files.Any())
        {
            MessageBox.Show("Tick local files to upload.", "No Selection",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        await UploadFilesAsync(files);
    }

    private async Task UploadFilesAsync(List<FileInfo> files)
    {
        var user = _authService.GetCurrentUser();
        if (user == null) return;

        _currentOperationCts = new CancellationTokenSource();
        int done = 0, total = files.Count;
        try
        {
            _btnUpload.Enabled = false;
            _btnCancel.Visible = true;
            ProgressVisible    = true;

            foreach (var f in files)
            {
                if (_currentOperationCts.Token.IsCancellationRequested) break;
                var key = _s3CurrentPrefix + f.Name;
                SafeSetStatus($"Uploading  {f.Name}  ({done + 1}/{total})");
                var prog = new Progress<double>(pct => SafeSetProgress((int)pct));
                await _s3Service.UploadFileAsync(f.FullName, key,
                    new List<UserRole> { user.Role }, prog, _currentOperationCts.Token);
                done++;
                SafeSetProgress(done * 100 / total);
            }

            StatusMessage = $"Uploaded {done}/{total} file(s)";
            await LoadS3ListAsync(_s3CurrentPrefix);
        }
        catch (OperationCanceledException) { StatusMessage = $"Upload cancelled  ({done}/{total})"; }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Upload Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally
        {
            _btnCancel.Visible = false;
            ProgressVisible    = false;
            _currentOperationCts = null;
            UpdateToolbarButtons();
        }
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════
    #region Download

    private async void OnDownloadSelected(object? sender, EventArgs e)
    {
        var nodes = GetCheckedS3Nodes().Where(n => !n.IsDirectory).ToList();
        if (!nodes.Any())
        {
            MessageBox.Show("Tick S3 files to download.", "No Selection",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var dest = _localCurrentPath;
        if (string.IsNullOrEmpty(dest))
        {
            using var dlg = new FolderBrowserDialog { Description = "Select download destination" };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            dest = dlg.SelectedPath;
        }

        await DownloadNodesAsync(nodes, dest);
    }

    private async Task DownloadNodesAsync(List<FileNode> nodes, string destFolder)
    {
        _currentOperationCts = new CancellationTokenSource();
        int done = 0, total = nodes.Count;
        try
        {
            _btnDownload.Enabled = false;
            _btnCancel.Visible   = true;
            ProgressVisible      = true;

            foreach (var node in nodes)
            {
                if (_currentOperationCts.Token.IsCancellationRequested) break;
                var localPath = Path.Combine(destFolder, node.Name);
                SafeSetStatus($"Downloading  {node.Name}  ({done + 1}/{total})");
                var prog = new Progress<double>(pct => SafeSetProgress((int)pct));
                await _s3Service.DownloadFileAsync(node.Path, localPath, prog, _currentOperationCts.Token);
                done++;
                SafeSetProgress(done * 100 / total);
            }

            StatusMessage = $"Downloaded {done}/{total} file(s)";
            if (!string.IsNullOrEmpty(_localCurrentPath)) LoadLocalFolder(_localCurrentPath);
        }
        catch (OperationCanceledException) { StatusMessage = $"Download cancelled  ({done}/{total})"; }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Download Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally
        {
            _btnCancel.Visible   = false;
            ProgressVisible      = false;
            _currentOperationCts = null;
            UpdateToolbarButtons();
        }
    }

    private async void OnDownloadAsZip(object? sender, EventArgs e)
    {
        var nodes = GetCheckedS3Nodes().Where(n => !n.IsDirectory).ToList();
        if (!nodes.Any()) { MessageBox.Show("Tick files to zip.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }

        using var dlg = new SaveFileDialog { Filter = "ZIP|*.zip", FileName = "download.zip" };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        _currentOperationCts = new CancellationTokenSource();
        ProgressVisible = true;
        try
        {
            var prog = new Progress<double>(pct => SafeSetProgress((int)pct));
            await _s3Service.DownloadAsZipAsync(nodes.Select(n => n.Path), dlg.FileName,
                prog, _currentOperationCts.Token);
            StatusMessage = $"ZIP saved: {dlg.FileName}";
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { ProgressVisible = false; _currentOperationCts = null; }
    }

    private async void OnDownloadFolder(object? sender, EventArgs e)
    {
        var folder = GetCheckedS3Nodes().FirstOrDefault(n => n.IsDirectory)
                  ?? _s3ListView.SelectedItems.Cast<ListViewItem>()
                                .Select(i => i.Tag as FileNode)
                                .FirstOrDefault(n => n?.IsDirectory == true);
        if (folder == null) { MessageBox.Show("Select an S3 folder.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }

        using var dlg = new FolderBrowserDialog { Description = "Select destination" };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        _currentOperationCts = new CancellationTokenSource();
        ProgressVisible = true;
        try
        {
            var prog = new Progress<double>(pct => SafeSetProgress((int)pct));
            await _s3Service.DownloadFolderAsync(folder.Path, dlg.SelectedPath, prog, _currentOperationCts.Token);
            StatusMessage = "Folder download complete";
            if (!string.IsNullOrEmpty(_localCurrentPath)) LoadLocalFolder(_localCurrentPath);
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { ProgressVisible = false; _currentOperationCts = null; }
    }

    private async Task DownloadFileAsync(string s3Key, string localPath)
    {
        ProgressVisible = true;
        try
        {
            var prog = new Progress<double>(pct => SafeSetProgress((int)pct));
            await _s3Service.DownloadFileAsync(s3Key, localPath, prog);
        }
        finally { ProgressVisible = false; }
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════
    #region S3 mutations

    private async void OnDeleteFromS3(object? sender, EventArgs e)
    {
        var nodes = GetCheckedS3Nodes().Where(n => !n.IsDirectory).ToList();
        if (!nodes.Any()) { MessageBox.Show("Tick files to delete.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        if (MessageBox.Show($"Delete {nodes.Count} file(s) from S3?", "Confirm Delete",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        ProgressVisible = true;
        try
        {
            await _s3Service.DeleteFilesAsync(nodes.Select(n => n.Path), CancellationToken.None);
            StatusMessage = $"Deleted {nodes.Count} file(s)";
            await LoadS3ListAsync(_s3CurrentPrefix);
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { ProgressVisible = false; }
    }

    private async void OnCopyS3Url(object? sender, EventArgs e)
    {
        var node = _s3ListView.SelectedItems.Cast<ListViewItem>()
                              .Select(i => i.Tag as FileNode)
                              .FirstOrDefault(n => n != null && !n.IsDirectory);
        if (node == null) return;
        var url = await _s3Service.GetPresignedUrlAsync(node.Path, TimeSpan.FromHours(1));
        if (url != null) { Clipboard.SetText(url); StatusMessage = "Presigned URL copied (expires 1h)"; }
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════
    #region Context menu helpers

    private void OnOpenInExplorer(object? sender, EventArgs e)
    {
        var path = _localListView.SelectedItems.Count > 0
            ? _localListView.SelectedItems[0].Tag switch
            {
                FileInfo f    => f.DirectoryName!,
                DirectoryInfo d => d.FullName,
                _ => _localCurrentPath
            }
            : _localCurrentPath;
        if (!string.IsNullOrEmpty(path)) Process.Start("explorer.exe", path);
    }

    private void OnShowLocalProperties(object? sender, EventArgs e)
    {
        if (_localListView.SelectedItems.Count == 0) return;
        var info = _localListView.SelectedItems[0].Tag switch
        {
            FileInfo f      => $"Name: {f.Name}\nSize: {FormatSize(f.Length)}\nModified: {f.LastWriteTime:g}\nPath: {f.FullName}",
            DirectoryInfo d => $"Name: {d.Name}\nModified: {d.LastWriteTime:g}\nPath: {d.FullName}",
            _ => ""
        };
        if (!string.IsNullOrEmpty(info))
            MessageBox.Show(info, "Properties", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void OnShowS3Properties(object? sender, EventArgs e)
    {
        if (_s3ListView.SelectedItems.Count == 0) return;
        if (_s3ListView.SelectedItems[0].Tag is FileNode n)
            MessageBox.Show(
                $"Name: {n.Name}\nKey: {n.Path}\nSize: {FormatSize(n.Size)}\nModified: {n.LastModified:g}\nVersion: {n.VersionId}",
                "Properties", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════
    #region Selection helpers

    private List<FileInfo> GetCheckedLocalFiles()
    {
        var list = new List<FileInfo>();
        foreach (ListViewItem item in _localListView.CheckedItems)
            if (item.Tag is FileInfo f) list.Add(f);
        return list;
    }

    private List<FileNode> GetCheckedS3Nodes()
    {
        var list = new List<FileNode>();
        foreach (ListViewItem item in _s3ListView.CheckedItems)
            if (item.Tag is FileNode n) list.Add(n);
        return list;
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════
    #region Utility

    private void ShowSettingsDialog() =>
        MessageBox.Show(
            "Configure sync options, bandwidth throttling, and file exclusions.",
            "Settings", MessageBoxButtons.OK, MessageBoxIcon.Information);

    private void CancelCurrentOperation()
    {
        _currentOperationCts?.Cancel();
        StatusMessage = "Cancelling…";
    }

    private void ShowErrorSummary(List<SyncError> errors)
    {
        var msg = string.Join("\n", errors.Take(10).Select(err => $"• {err.FilePath}: {err.Message}"));
        if (errors.Count > 10) msg += $"\n…and {errors.Count - 10} more";
        MessageBox.Show($"Some files failed:\n\n{msg}", "Sync Errors", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private static string FormatSize(long bytes)
    {
        string[] u = { "B", "KB", "MB", "GB", "TB" };
        int i = 0; double v = bytes;
        while (v >= 1024 && i < u.Length - 1) { i++; v /= 1024; }
        return $"{v:0.##} {u[i]}";
    }

    private void SafeSetStatus(string text)
    {
        if (InvokeRequired) Invoke(() => _statusLabel.Text = text);
        else _statusLabel.Text = text;
    }

    private void SafeSetProgress(int value)
    {
        if (InvokeRequired) Invoke(() => _progressBar.Value = Math.Clamp(value, 0, 100));
        else _progressBar.Value = Math.Clamp(value, 0, 100);
    }

    private void SafeSetProgressVisible(bool visible)
    {
        if (InvokeRequired) Invoke(() => _progressBar.Visible = visible);
        else _progressBar.Visible = visible;
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════
    #region IFileSyncView

    public void UpdateLocalTree(List<FileNode> nodes) => LoadLocalFolder(_localCurrentPath);
    public void UpdateRemoteTree(List<FileNode> nodes) => _ = LoadS3ListAsync(_s3CurrentPrefix);
    public void ShowProgress(string message, int percentage)
    {
        SafeSetStatus(message);
        SafeSetProgress(percentage);
    }

    #endregion
}

// ══════════════════════════════════════════════════════════════════════════
/// <summary>Dialog for resolving file conflicts</summary>
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
        Width = 820; Height = 520;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;

        var label = new Label
        {
            Text = $"{_conflicts.Count} file(s) modified on both sides. Choose a resolution for each:",
            Location = new Point(10, 10), Size = new Size(780, 40)
        };

        _grid = new DataGridView
        {
            Location = new Point(10, 55), Size = new Size(780, 360),
            AutoGenerateColumns = false, AllowUserToAddRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };
        _grid.Columns.AddRange(new DataGridViewColumn[]
        {
            new DataGridViewTextBoxColumn { HeaderText = "File",            Width = 240, DataPropertyName = "FileName" },
            new DataGridViewTextBoxColumn { HeaderText = "Local Modified",  Width = 130, DataPropertyName = "LocalModified" },
            new DataGridViewTextBoxColumn { HeaderText = "Remote Modified", Width = 130, DataPropertyName = "RemoteModified" },
            new DataGridViewComboBoxColumn
            {
                HeaderText = "Resolution", Width = 160, DataPropertyName = "Resolution",
                Items = { "Keep Local", "Keep Remote", "Keep Both", "Skip" }
            }
        });

        var btnApply  = new Button { Text = "Apply",  Location = new Point(625, 430), Size = new Size(80, 30), DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "Cancel", Location = new Point(715, 430), Size = new Size(80, 30), DialogResult = DialogResult.Cancel };
        Controls.AddRange(new Control[] { label, _grid, btnApply, btnCancel });
        AcceptButton = btnApply; CancelButton = btnCancel;
    }

    private void LoadConflicts()
    {
        _grid.DataSource = _conflicts.Select(c => new ConflictViewModel
        {
            FileName       = Path.GetFileName(c.LocalPath),
            LocalModified  = c.LocalModified.ToString("g"),
            RemoteModified = c.RemoteModified.ToString("g"),
            Resolution     = "Keep Local",
            Conflict       = c
        }).ToList();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (DialogResult == DialogResult.OK && _grid.DataSource is List<ConflictViewModel> list)
        {
            foreach (var row in list)
                row.Conflict.Resolution = row.Resolution switch
                {
                    "Keep Local"   => ConflictResolution.KeepLocal,
                    "Keep Remote"  => ConflictResolution.KeepRemote,
                    "Keep Both"    => ConflictResolution.KeepBoth,
                    _              => ConflictResolution.Skip
                };
        }
        base.OnFormClosing(e);
    }

    private class ConflictViewModel
    {
        public string       FileName       { get; set; } = "";
        public string       LocalModified  { get; set; } = "";
        public string       RemoteModified { get; set; } = "";
        public string       Resolution     { get; set; } = "";
        public ConflictInfo Conflict       { get; set; } = null!;
    }
}

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
    private KryptonButton _btnSync      = null!;
    private KryptonButton _btnUpload    = null!;
    private KryptonButton _btnDownload  = null!;
    private KryptonButton _btnNewFolder = null!;
    private KryptonButton _btnRefresh   = null!;
    private KryptonButton _btnSettings  = null!;
    private KryptonButton _btnSchedules = null!;
    private Button        _btnCancel    = null!;

    // ── Profile selector ─────────────────────────────────────────────────
    private ComboBox _cmbProfile = null!;

    // ── Local pane ────────────────────────────────────────────────────────
    private KryptonTextBox _txtLocalPath      = null!;
    private KryptonButton  _btnBrowseLocal    = null!;
    private KryptonButton  _btnLocalUp        = null!;
    private KryptonButton  _btnLocalSelectAll = null!;
    private ListView       _localListView     = null!;
    private TextBox        _txtLocalFilter    = null!;

    // ── S3 pane ───────────────────────────────────────────────────────────
    private KryptonTextBox _txtS3Prefix      = null!;
    private KryptonButton  _btnS3Up          = null!;
    private KryptonButton  _btnS3Refresh     = null!;
    private KryptonButton  _btnS3SelectAll   = null!;
    private ListView       _s3ListView       = null!;
    private TextBox        _txtS3Filter      = null!;

    // ── Status bar────────────────────────────────────────────────────────
    private KryptonLabel       _statusLabel = null!;
    private KryptonProgressBar _progressBar = null!;

    // ── Layout ────────────────────────────────────────────────────────────
    private SplitContainer _split = null!;

    // ── Column sorters ────────────────────────────────────────────────────
    private readonly ListViewColumnSorter _localSorter = new();
    private readonly ListViewColumnSorter _s3Sorter    = new();

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
    private readonly ISyncSchedulerService? _schedulerService;

    public MainForm(
        IAuthService authService,
        IFileStorageService s3Service,
        IConfigurationService configService,
        ISyncEngine syncEngine,
        ISyncSchedulerService? schedulerService = null)
    {
        _authService       = authService;
        _s3Service         = s3Service;
        _configService     = configService;
        _syncEngine        = syncEngine;
        _schedulerService  = schedulerService;

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

        _btnSync      = ToolBtn("⇄  Sync Now",      Color.FromArgb(0, 122, 204));
        _btnUpload    = ToolBtn("⬆  Upload →",      Color.FromArgb(30, 120, 0));
        _btnDownload  = ToolBtn("← ⬇  Download",   Color.FromArgb(0, 100, 170));
        _btnNewFolder = ToolBtn("📁  New Folder",    Color.FromArgb(60, 60, 90));
        _btnRefresh   = ToolBtn("↺  Refresh",       Color.FromArgb(70, 70, 80));
        _btnSettings  = ToolBtn("⚙  Settings",      Color.FromArgb(70, 70, 80));
        _btnSchedules = ToolBtn("⏱  Schedules",     Color.FromArgb(90, 60, 110));

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
        foreach (Control btn in new Control[] { _btnSync, _btnUpload, _btnDownload, _btnNewFolder, _btnRefresh, _btnSettings, _btnSchedules, _btnCancel })
        {
            btn.Location = new Point(bx, 9);
            btn.Size     = new Size(110, 38);
            bx += 116;
        }

        // ── Profile selector (right side of toolbar) ─────────────────────
        _cmbProfile = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font          = new Font("Segoe UI", 8.5f),
            BackColor     = Color.FromArgb(60, 60, 65),
            ForeColor     = Color.White,
            Width         = 160,
            Height        = 28,
        };
        _cmbProfile.Location = new Point(Width - 200, 13);
        _cmbProfile.SelectedIndexChanged += OnProfileChanged;

        toolbar.Controls.AddRange(new Control[] { _btnSync, _btnUpload, _btnDownload, _btnNewFolder, _btnRefresh, _btnSettings, _btnSchedules, _btnCancel, _cmbProfile });

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
        _btnLocalSelectAll = new KryptonButton
        {
            Text     = "☑ All",
            Location = new Point(726, 11),
            Size     = new Size(68, 30)
        };
        _btnLocalSelectAll.ToolTipValues.Description = "Select / deselect all (Ctrl+A)";

        header.Controls.AddRange(new Control[] { lbl, _txtLocalPath, _btnBrowseLocal, _btnLocalUp, _btnLocalSelectAll });

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

        _localListView.ListViewItemSorter = _localSorter;

        var localFilterBar = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 32,
            BackColor = Color.FromArgb(40, 40, 40)
        };
        _txtLocalFilter = new TextBox
        {
            Location        = new Point(6, 4),
            Size            = new Size(300, 24),
            PlaceholderText = "🔍  Filter files… (Ctrl+F)"
        };
        localFilterBar.Controls.Add(_txtLocalFilter);

        pane.Controls.Add(_localListView);
        pane.Controls.Add(localFilterBar);
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
        _btnS3SelectAll = new KryptonButton
        {
            Text     = "☑ All",
            Location = new Point(686, 11),
            Size     = new Size(68, 30)
        };
        _btnS3SelectAll.ToolTipValues.Description = "Select / deselect all (Ctrl+A)";

        header.Controls.AddRange(new Control[] { lbl, _txtS3Prefix, _btnS3Up, _btnS3Refresh, _btnS3SelectAll });

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

        _s3ListView.ListViewItemSorter = _s3Sorter;

        var s3FilterBar = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 32,
            BackColor = Color.FromArgb(20, 30, 50)
        };
        _txtS3Filter = new TextBox
        {
            Location        = new Point(6, 4),
            Size            = new Size(300, 24),
            PlaceholderText = "🔍  Filter files… (Ctrl+F)"
        };
        s3FilterBar.Controls.Add(_txtS3Filter);

        pane.Controls.Add(_s3ListView);
        pane.Controls.Add(s3FilterBar);
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
            new ToolStripMenuItem("Rename",             null, OnRenameLocal),
            new ToolStripSeparator(),
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
            new ToolStripMenuItem("Rename",             null, (s, e) => MessageBox.Show("S3 rename not yet supported.", "Not Supported", MessageBoxButtons.OK, MessageBoxIcon.Information)) { Enabled = true },
            new ToolStripSeparator(),
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
        _btnSync.Click      += async (s, e) => await PerformSyncAsync();
        _btnUpload.Click    += OnUploadSelected;
        _btnDownload.Click  += OnDownloadSelected;
        _btnRefresh.Click   += async (s, e) => { LoadLocalFolder(_localCurrentPath); await LoadS3ListAsync(_s3CurrentPrefix); };
        _btnSettings.Click  += (s, e) => ShowSettingsDialog();
        _btnSchedules.Click += (s, e) => ShowSchedulesDialog();
        _btnCancel.Click    += (s, e) => CancelCurrentOperation();

        _btnBrowseLocal.Click    += (s, e) => BrowseForLocalFolder();
        _btnLocalUp.Click        += (s, e) => NavigateLocalUp();
        _btnLocalSelectAll.Click += (s, e) => ToggleSelectAll(_localListView);
        _btnS3Up.Click           += async (s, e) => await NavigateS3UpAsync();
        _btnS3Refresh.Click      += async (s, e) => await LoadS3ListAsync(_s3CurrentPrefix);
        _btnS3SelectAll.Click    += (s, e) => ToggleSelectAll(_s3ListView);

        _localListView.ItemChecked += OnLocalItemChecked;
        _localListView.DoubleClick += LocalList_DoubleClick;
        _s3ListView.ItemChecked    += OnS3ItemChecked;
        _s3ListView.DoubleClick    += S3List_DoubleClick;

        _btnNewFolder.Click += OnNewFolder;

        _txtLocalFilter.TextChanged += (s, e) => ApplyLocalFilter();
        _txtS3Filter.TextChanged    += (s, e) => ApplyS3Filter();

        _localListView.ColumnClick += (s, e) => SortListView(_localListView, _localSorter, e.Column);
        _s3ListView.ColumnClick    += (s, e) => SortListView(_s3ListView,    _s3Sorter,    e.Column);

        // Drag local items onto S3 pane + Explorer drag-drop
        _localListView.ItemDrag  += (s, e) => _localListView.DoDragDrop(e.Item!, DragDropEffects.Copy);
        _localListView.AllowDrop  = true;
        _localListView.DragEnter += (s, e) =>
        {
            if (e.Data!.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        };
        _localListView.DragDrop  += (s, e) =>
        {
            if (e.Data!.GetData(DataFormats.FileDrop) is string[] paths && !string.IsNullOrEmpty(_localCurrentPath))
            {
                foreach (var src in paths)
                {
                    var dst = Path.Combine(_localCurrentPath, Path.GetFileName(src)!);
                    try
                    {
                        if (File.Exists(src))           File.Copy(src, dst, overwrite: true);
                        else if (Directory.Exists(src)) CopyDirectory(src, dst);
                    }
                    catch (Exception ex) { SafeSetStatus($"Copy error: {ex.Message}"); }
                }
                LoadLocalFolder(_localCurrentPath);
            }
        };
        _s3ListView.AllowDrop    = true;
        _s3ListView.DragEnter   += (s, e) =>
        {
            if (e.Data!.GetDataPresent(DataFormats.FileDrop) || e.Data!.GetDataPresent(typeof(ListViewItem)))
                e.Effect = DragDropEffects.Copy;
        };
        _s3ListView.DragDrop    += (s, e) =>
        {
            if (e.Data!.GetData(DataFormats.FileDrop) is string[] paths)
            {
                var uploadItems = new List<(FileInfo File, string RelativePath)>();
                foreach (var p in paths)
                {
                    if (File.Exists(p))
                        uploadItems.Add((new FileInfo(p), Path.GetFileName(p)!));
                    else if (Directory.Exists(p))
                    {
                        var di = new DirectoryInfo(p);
                        foreach (var f in di.EnumerateFiles("*", SearchOption.AllDirectories))
                            uploadItems.Add((f, Path.GetRelativePath(di.Parent!.FullName, f.FullName).Replace('\\', '/')));
                    }
                }
                if (uploadItems.Count > 0)
                    _ = UploadItemsDirectAsync(uploadItems);
            }
            else
            {
                OnUploadSelected(s, EventArgs.Empty);
            }
        };

        _syncEngine.ConflictsDetected += OnConflictsDetected;
    }

    // Checks all items if any are unchecked; unchecks all if all are checked.
    private void ToggleSelectAll(ListView lv)
    {
        bool anyUnchecked = lv.Items.Cast<ListViewItem>().Any(i => i.Tag != null && !i.Checked);
        lv.BeginUpdate();
        foreach (ListViewItem item in lv.Items)
        {
            if (item.Tag != null)
                item.Checked = anyUnchecked;
        }
        lv.EndUpdate();
        UpdateToolbarButtons();
    }

    private void OnLocalItemChecked(object? sender, ItemCheckedEventArgs e)
    {
        if (e.Item.Tag is DirectoryInfo dir)
            SetStatus($"Folder '{dir.Name}' {(e.Item.Checked ? "selected" : "deselected")} — all contents will be included.");
        UpdateToolbarButtons();
    }

    private void OnS3ItemChecked(object? sender, ItemCheckedEventArgs e)
    {
        if (e.Item.Tag is FileNode { IsDirectory: true } node)
            SetStatus($"S3 folder '{node.Name}' {(e.Item.Checked ? "selected" : "deselected")} — all contents will be included.");
        UpdateToolbarButtons();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.A))
        {
            if (_localListView.Focused)
            {
                ToggleSelectAll(_localListView);
                return true;
            }
            if (_s3ListView.Focused)
            {
                ToggleSelectAll(_s3ListView);
                return true;
            }
        }
        if (keyData == Keys.F5)
        {
            _ = Task.Run(async () => {
                LoadLocalFolder(_localCurrentPath);
                await LoadS3ListAsync(_s3CurrentPrefix);
            });
            return true;
        }
        if (keyData == (Keys.Control | Keys.F))
        {
            if (_localListView.Focused || _split.Panel1.ContainsFocus)
                _txtLocalFilter.Focus();
            else
                _txtS3Filter.Focus();
            return true;
        }
        if (keyData == (Keys.Control | Keys.U))
        {
            if (_btnUpload.Enabled) OnUploadSelected(null, EventArgs.Empty);
            return true;
        }
        if (keyData == (Keys.Control | Keys.D))
        {
            if (_btnDownload.Enabled) OnDownloadSelected(null, EventArgs.Empty);
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        BeginInvoke(() =>
        {
            if (_split.Width > 0)
                _split.SplitterDistance = _split.Width / 2;
        });

        // Populate profile selector
        PopulateProfileCombo();
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        await LoadS3ListAsync("");

        // Start background scheduler (fire-and-forget; non-fatal if it fails)
        if (_schedulerService is not null)
        {
            try { await _schedulerService.StartAsync(); }
            catch (Exception ex) { SafeSetStatus($"Scheduler init warning: {ex.Message}"); }
        }
    }

    private void UpdateToolbarButtons()
    {
        _btnUpload.Enabled   = _localListView.CheckedItems.Cast<ListViewItem>().Any(i => i.Tag != null);
        _btnDownload.Enabled = _s3ListView.CheckedItems.Cast<ListViewItem>().Any(i => i.Tag != null);
    }

    private void PopulateProfileCombo()
    {
        _cmbProfile.SelectedIndexChanged -= OnProfileChanged;
        _cmbProfile.Items.Clear();
        foreach (var p in _configService.GetProfiles())
            _cmbProfile.Items.Add(p.Name);

        var active = _configService.GetActiveProfile();
        if (active != null && _cmbProfile.Items.Contains(active.Name))
            _cmbProfile.SelectedItem = active.Name;
        else if (_cmbProfile.Items.Count > 0)
            _cmbProfile.SelectedIndex = 0;

        _cmbProfile.SelectedIndexChanged += OnProfileChanged;
    }

    private async void OnProfileChanged(object? sender, EventArgs e)
    {
        if (_cmbProfile.SelectedItem is not string name) return;
        _configService.SetActiveProfile(name);

        // Invalidate S3 client so it re-connects with new credentials
        if (_s3Service is FileSyncApp.S3.Services.S3FileStorageService svc)
            svc.InvalidateClient();

        var profile = _configService.GetActiveProfile();
        if (profile != null)
            Text = $"FileSyncApp – {profile.BucketName}";

        SafeSetStatus($"Switched to profile: {name}");
        await LoadS3ListAsync("");
    }

    private void ShowSchedulesDialog()
    {
        using var dlg = new ScheduleForm(_configService, _schedulerService);
        dlg.ShowDialog(this);
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
        var localFiles = _localListView.Items.Cast<ListViewItem>()
            .Where(i => i.Tag is FileInfo).ToList();
        var localDirs = _localListView.Items.Cast<ListViewItem>()
            .Count(i => i.Tag is DirectoryInfo);
        var localSize = localFiles.Sum(i => ((FileInfo)i.Tag!).Length);
        StatusMessage = $"Local: {localDirs} folder(s), {localFiles.Count} file(s)  •  {FormatSize(localSize)} total";
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

            var files     = items.Count(n => !n.IsDirectory);
            var folders   = items.Count(n => n.IsDirectory);
            var totalSize = items.Where(n => !n.IsDirectory).Sum(n => n.Size);
            StatusMessage = $"S3: {folders} folder(s), {files} file(s)  •  {FormatSize(totalSize)} total";
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

        // Smart direction: if only one side has checked items, route to upload/download
        bool hasLocalChecked = _localListView.CheckedItems.Cast<ListViewItem>().Any(i => i.Tag != null);
        bool hasS3Checked    = _s3ListView.CheckedItems.Cast<ListViewItem>().Any(i => i.Tag != null);

        if (hasLocalChecked && !hasS3Checked)
        {
            // Local items selected → upload
            OnUploadSelected(null, EventArgs.Empty);
            return;
        }
        if (hasS3Checked && !hasLocalChecked)
        {
            // S3 items selected → download
            OnDownloadSelected(null, EventArgs.Empty);
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
            _currentOperationCts?.Dispose();
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
        using var dlg = new ConflictResolutionForm(e.Conflicts);
        if (dlg.ShowDialog(this) == DialogResult.OK) e.Handled = true;
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════
    #region Upload

    private async void OnUploadSelected(object? sender, EventArgs e)
    {
        if (!_localListView.CheckedItems.Cast<ListViewItem>().Any(i => i.Tag != null))
        {
            MessageBox.Show("Tick local files or folders to upload.", "No Selection",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _currentOperationCts = new CancellationTokenSource();
        var ct = _currentOperationCts.Token;

        try
        {
            _btnUpload.Enabled = false;
            _btnCancel.Visible = true;
            ProgressVisible    = true;
            SafeSetStatus("Expanding folder selection…");

            var items = ExpandLocalCheckedItems();
            if (!items.Any()) { SafeSetStatus("Nothing to upload."); return; }

            SafeSetStatus("Checking for changes…");
            var toUpload = await FilterChangedUploadsAsync(items, ct);

            if (!toUpload.Any())
            {
                SafeSetStatus("All selected files are already up to date in S3.");
                return;
            }

            int done = 0, total = toUpload.Count;
            var user = _authService.GetCurrentUser();
            if (user == null) return;

            var tasks = toUpload.Select(async item =>
            {
                if (ct.IsCancellationRequested) return;
                var (file, relPath) = item;
                var key = (_s3CurrentPrefix.TrimEnd('/') + "/" + relPath).TrimStart('/');
                SafeSetStatus($"Uploading  {relPath}  ({Interlocked.Increment(ref done)}/{total})");
                var prog = new Progress<double>(pct => SafeSetProgress((int)pct));
                await _s3Service.UploadFileAsync(file.FullName, key,
                    new List<UserRole> { user.Role }, prog, ct);
                SafeSetProgress(done * 100 / total);
            }).ToList();

            await Task.WhenAll(tasks);

            StatusMessage = $"Uploaded {done}/{total} file(s) — skipped {items.Count - done} unchanged";
            await LoadS3ListAsync(_s3CurrentPrefix);
        }
        catch (OperationCanceledException) { StatusMessage = "Upload cancelled"; }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Upload Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally
        {
            _btnCancel.Visible   = false;
            ProgressVisible      = false;
            _currentOperationCts?.Dispose();
            _currentOperationCts = null;
            UpdateToolbarButtons();
        }
    }

    // Recursively expands any checked DirectoryInfo tags into their constituent files.
    private List<(FileInfo File, string RelativePath)> ExpandLocalCheckedItems()
    {
        var result = new List<(FileInfo, string)>();
        var baseDir = string.IsNullOrEmpty(_localCurrentPath) ? "" : _localCurrentPath;

        foreach (ListViewItem item in _localListView.CheckedItems)
        {
            switch (item.Tag)
            {
                case FileInfo f:
                    result.Add((f, Path.GetRelativePath(baseDir, f.FullName).Replace('\\', '/')));
                    break;
                case DirectoryInfo d:
                    ExpandLocalDirectory(d, baseDir, result);
                    break;
            }
        }
        return result;
    }

    private static void ExpandLocalDirectory(DirectoryInfo dir, string baseDir, List<(FileInfo, string)> result)
    {
        try
        {
            foreach (var f in dir.EnumerateFiles("*", SearchOption.AllDirectories))
                result.Add((f, Path.GetRelativePath(baseDir, f.FullName).Replace('\\', '/')));
        }
        catch (UnauthorizedAccessException) { /* skip inaccessible dirs */ }
    }

    // Returns only those files whose remote counterpart is absent or older/different-size.
    private async Task<List<(FileInfo File, string RelativePath)>> FilterChangedUploadsAsync(
        List<(FileInfo File, string RelativePath)> candidates,
        CancellationToken ct)
    {
        // Fetch the full recursive S3 listing for the current prefix once.
        var remoteFiles = await _s3Service.ListAllFilesRecursiveAsync(
            _authService.GetCurrentUser()?.Role ?? UserRole.User,
            _s3CurrentPrefix, ct);

        var remoteByKey = remoteFiles.ToDictionary(
            n => n.Path.TrimStart('/'),
            n => n,
            StringComparer.OrdinalIgnoreCase);

        var changed = new List<(FileInfo, string)>();
        var basePrefix = _s3CurrentPrefix.TrimEnd('/');

        foreach (var (file, relPath) in candidates)
        {
            var key = (basePrefix + "/" + relPath).TrimStart('/');
            if (!remoteByKey.TryGetValue(key, out var remote))
            {
                changed.Add((file, relPath));
                continue;
            }
            // Upload if size differs or local is newer (with 2-second grace for clock drift)
            if (remote.Size != file.Length ||
                file.LastWriteTimeUtc > remote.LastModified.ToUniversalTime() + TimeSpan.FromSeconds(2))
            {
                changed.Add((file, relPath));
            }
        }
        return changed;
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════
    #region Download

    private async void OnDownloadSelected(object? sender, EventArgs e)
    {
        if (!_s3ListView.CheckedItems.Cast<ListViewItem>().Any(i => i.Tag != null))
        {
            MessageBox.Show("Tick S3 files or folders to download.", "No Selection",
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

        _currentOperationCts = new CancellationTokenSource();
        var ct = _currentOperationCts.Token;

        try
        {
            _btnDownload.Enabled = false;
            _btnCancel.Visible   = true;
            ProgressVisible      = true;
            SafeSetStatus("Expanding folder selection…");

            var nodes = await ExpandS3CheckedItemsAsync(ct);
            if (!nodes.Any()) { SafeSetStatus("Nothing to download."); return; }

            SafeSetStatus("Checking for changes…");
            var toDownload = FilterChangedDownloads(nodes, dest);

            if (!toDownload.Any())
            {
                SafeSetStatus("All selected files are already up to date locally.");
                return;
            }

            int done = 0, skipped = 0, total = toDownload.Count;
            var errors = new List<string>();

            foreach (var node in toDownload)
            {
                if (ct.IsCancellationRequested) break;
                // Reconstruct relative path from the S3 key by stripping current prefix
                var rel  = node.Path.TrimStart('/');
                var pref = _s3CurrentPrefix.TrimStart('/');
                if (rel.StartsWith(pref, StringComparison.OrdinalIgnoreCase))
                    rel = rel[pref.Length..].TrimStart('/');

                var localPath = Path.Combine(dest, rel.Replace('/', Path.DirectorySeparatorChar));

                // Skip if local path is already a directory (S3 folder-placeholder object)
                if (Directory.Exists(localPath)) { skipped++; continue; }

                var parentDir = Path.GetDirectoryName(localPath);
                if (!string.IsNullOrEmpty(parentDir)) Directory.CreateDirectory(parentDir);

                SafeSetStatus($"Downloading  {rel}  ({done + 1}/{total})");
                var prog = new Progress<double>(pct => SafeSetProgress((int)pct));

                try
                {
                    await _s3Service.DownloadFileAsync(node.Path, localPath, prog, ct);
                    done++;
                    SafeSetProgress(done * 100 / total);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    skipped++;
                    errors.Add($"{rel}: {ex.Message}");
                }
            }

            var summary = $"Downloaded {done}/{total} file(s)";
            if (skipped > 0) summary += $" — {skipped} skipped";
            StatusMessage = summary;
            if (errors.Any())
            {
                var msg = string.Join("\n", errors.Take(10));
                if (errors.Count > 10) msg += $"\n… and {errors.Count - 10} more";
                MessageBox.Show(msg, "Some files could not be downloaded", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            if (!string.IsNullOrEmpty(_localCurrentPath)) LoadLocalFolder(_localCurrentPath);
        }
        catch (OperationCanceledException) { StatusMessage = "Download cancelled"; }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Download Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally
        {
            _btnCancel.Visible   = false;
            ProgressVisible      = false;
            _currentOperationCts?.Dispose();
            _currentOperationCts = null;
            UpdateToolbarButtons();
        }
    }

    // Expands checked S3 folder items by recursively listing their contents.
    private async Task<List<FileNode>> ExpandS3CheckedItemsAsync(CancellationToken ct)
    {
        var result = new List<FileNode>();
        var userRole = _authService.GetCurrentUser()?.Role ?? UserRole.User;

        foreach (ListViewItem item in _s3ListView.CheckedItems)
        {
            if (item.Tag is not FileNode node) continue;

            if (node.IsDirectory)
            {
                var children = await _s3Service.ListAllFilesRecursiveAsync(userRole, node.Path, ct);
                result.AddRange(children);
            }
            else
            {
                result.Add(node);
            }
        }
        return result;
    }

    private List<FileNode> FilterChangedDownloads(List<FileNode> nodes, string destFolder)
    {
        var changed = new List<FileNode>();
        var pref    = _s3CurrentPrefix.TrimStart('/');
        foreach (var node in nodes)
        {
            // Compute relative path the same way the download loop does (strip current prefix)
            var rel = node.Path.TrimStart('/');
            if (rel.StartsWith(pref, StringComparison.OrdinalIgnoreCase))
                rel = rel[pref.Length..].TrimStart('/');
            var localPath = Path.Combine(destFolder, rel.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(localPath))
            {
                changed.Add(node);
                continue;
            }
            var info = new FileInfo(localPath);
            if (info.Length != node.Size ||
                info.LastWriteTimeUtc < node.LastModified.ToUniversalTime() - TimeSpan.FromSeconds(2))
            {
                changed.Add(node);
            }
        }
        return changed;
    }

    #endregion

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
        finally { ProgressVisible = false; _currentOperationCts?.Dispose(); _currentOperationCts = null; }
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
        finally { ProgressVisible = false; _currentOperationCts?.Dispose(); _currentOperationCts = null; }
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

    private void ShowSettingsDialog()
    {
        using var dlg = new SettingsForm(_configService);
        dlg.ShowDialog(this);
    }

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

    private void SetStatus(string text) => SafeSetStatus(text);

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
    #region Filter / Sort helpers

    private void ApplyLocalFilter()
    {
        var term = _txtLocalFilter.Text.Trim().ToLowerInvariant();
        _localListView.BeginUpdate();
        foreach (ListViewItem item in _localListView.Items)
        {
            if (item.Tag == null) { item.BackColor = Color.Empty; continue; }
            var name = item.Tag switch
            {
                FileInfo f      => f.Name.ToLowerInvariant(),
                DirectoryInfo d => d.Name.ToLowerInvariant(),
                _               => item.Text.ToLowerInvariant()
            };
            item.BackColor = string.IsNullOrEmpty(term) || name.Contains(term)
                ? Color.Empty
                : Color.FromArgb(60, 0, 0);
        }
        _localListView.EndUpdate();
    }

    private void ApplyS3Filter()
    {
        var term = _txtS3Filter.Text.Trim().ToLowerInvariant();
        _s3ListView.BeginUpdate();
        foreach (ListViewItem item in _s3ListView.Items)
        {
            if (item.Tag == null || item.Tag is string) { item.BackColor = Color.Empty; continue; }
            var name = item.Tag is FileNode n ? n.Name.ToLowerInvariant() : item.Text.ToLowerInvariant();
            item.BackColor = string.IsNullOrEmpty(term) || name.Contains(term)
                ? Color.Empty
                : Color.FromArgb(0, 40, 60);
        }
        _s3ListView.EndUpdate();
    }

    private static void SortListView(ListView lv, ListViewColumnSorter sorter, int col)
    {
        if (sorter.SortColumn == col)
            sorter.Order = sorter.Order == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
        else
        {
            sorter.SortColumn = col;
            sorter.Order = SortOrder.Ascending;
        }
        lv.Sort();
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════
    #region New folder / Rename

    private static string? ShowInputDialog(string prompt, string title, string defaultValue = "")
    {
        using var form = new Form
        {
            Text = title, Width = 380, Height = 130,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false
        };
        var lbl = new Label { Text = prompt, Left = 10, Top = 15, Width = 340 };
        var txt = new TextBox { Left = 10, Top = 38, Width = 340, Text = defaultValue };
        var ok  = new Button { Text = "OK",     Left = 200, Top = 68, Width = 75, DialogResult = DialogResult.OK };
        var cn  = new Button { Text = "Cancel", Left = 285, Top = 68, Width = 75, DialogResult = DialogResult.Cancel };
        form.Controls.AddRange(new Control[] { lbl, txt, ok, cn });
        form.AcceptButton = ok;
        form.CancelButton = cn;
        return form.ShowDialog() == DialogResult.OK ? txt.Text.Trim() : null;
    }

    private void OnNewFolder(object? sender, EventArgs e)
    {
        var name = ShowInputDialog("New folder name:", "New Folder", "NewFolder");
        if (string.IsNullOrWhiteSpace(name)) return;
        name = name.Trim();

        if (!string.IsNullOrEmpty(_localCurrentPath))
        {
            try
            {
                var newPath = Path.Combine(_localCurrentPath, name);
                Directory.CreateDirectory(newPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not create local folder: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            LoadLocalFolder(_localCurrentPath);
        }

        _ = CreateS3FolderAsync(name);
    }

    private async Task CreateS3FolderAsync(string name)
    {
        try
        {
            var key = (_s3CurrentPrefix.TrimEnd('/') + "/" + name.Trim('/') + "/").TrimStart('/');
            var tmpFile = Path.GetTempFileName();
            try
            {
                var user = _authService.GetCurrentUser();
                await _s3Service.UploadFileAsync(tmpFile, key,
                    new List<UserRole> { user?.Role ?? UserRole.User },
                    null, CancellationToken.None);
            }
            finally { File.Delete(tmpFile); }
            await LoadS3ListAsync(_s3CurrentPrefix);
            SafeSetStatus($"Created S3 folder: {name}");
        }
        catch (Exception ex)
        {
            SafeSetStatus($"Could not create S3 folder: {ex.Message}");
        }
    }

    private void OnRenameLocal(object? sender, EventArgs e)
    {
        if (_localListView.SelectedItems.Count == 0) return;
        var item = _localListView.SelectedItems[0];
        if (item.Tag is not FileInfo fi) return;

        var newName = ShowInputDialog("New name:", "Rename", fi.Name);
        if (string.IsNullOrWhiteSpace(newName) || newName == fi.Name) return;
        try
        {
            var dest = Path.Combine(fi.DirectoryName!, newName.Trim());
            File.Move(fi.FullName, dest);
            LoadLocalFolder(_localCurrentPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Rename Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════
    #region Drag-drop helpers

    private static void CopyDirectory(string src, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(src))
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: true);
        foreach (var sub in Directory.GetDirectories(src))
            CopyDirectory(sub, Path.Combine(dest, Path.GetFileName(sub)));
    }

    private async Task UploadItemsDirectAsync(List<(FileInfo File, string RelativePath)> items)
    {
        var user = _authService.GetCurrentUser();
        if (user == null) return;

        _currentOperationCts = new CancellationTokenSource();
        var ct = _currentOperationCts.Token;
        int done = 0, total = items.Count;

        try
        {
            _btnCancel.Visible = true;
            ProgressVisible    = true;
            foreach (var (file, relPath) in items)
            {
                if (ct.IsCancellationRequested) break;
                var key = (_s3CurrentPrefix.TrimEnd('/') + "/" + relPath).TrimStart('/');
                SafeSetStatus($"Uploading {relPath} ({++done}/{total})");
                await _s3Service.UploadFileAsync(file.FullName, key,
                    new List<UserRole> { user.Role }, null, ct);
                SafeSetProgress(done * 100 / total);
            }
            StatusMessage = $"Uploaded {done}/{total} file(s)";
            await LoadS3ListAsync(_s3CurrentPrefix);
        }
        catch (OperationCanceledException) { StatusMessage = "Upload cancelled"; }
        catch (Exception ex) { SafeSetStatus($"Upload error: {ex.Message}"); }
        finally
        {
            _btnCancel.Visible = false;
            ProgressVisible    = false;
            _currentOperationCts?.Dispose();
            _currentOperationCts = null;
            UpdateToolbarButtons();
        }
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
/// <summary>Handles click-to-sort on a ListView column.</summary>
internal sealed class ListViewColumnSorter : System.Collections.IComparer
{
    public int       SortColumn { get; set; }
    public SortOrder Order      { get; set; } = SortOrder.None;

    public int Compare(object? x, object? y)
    {
        if (x is not ListViewItem lx || y is not ListViewItem ly) return 0;
        var tx = lx.SubItems.Count > SortColumn ? lx.SubItems[SortColumn].Text : "";
        var ty = ly.SubItems.Count > SortColumn ? ly.SubItems[SortColumn].Text : "";

        if (tx == "..") return -1;
        if (ty == "..") return  1;

        int result;
        if (SortColumn == 1)
        {
            result = ParseSize(tx).CompareTo(ParseSize(ty));
        }
        else if (SortColumn == 2)
        {
            result = DateTime.TryParse(tx, out var dx) && DateTime.TryParse(ty, out var dy)
                ? dx.CompareTo(dy)
                : string.Compare(tx, ty, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            result = string.Compare(tx, ty, StringComparison.OrdinalIgnoreCase);
        }
        return Order == SortOrder.Descending ? -result : result;
    }

    private static long ParseSize(string s)
    {
        if (string.IsNullOrWhiteSpace(s) || s == "—") return 0;
        var parts = s.Trim().Split(' ');
        if (parts.Length < 2 || !double.TryParse(parts[0], System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var v)) return 0;
        return parts[1].ToUpperInvariant() switch
        {
            "KB" => (long)(v * 1024),
            "MB" => (long)(v * 1024 * 1024),
            "GB" => (long)(v * 1024 * 1024 * 1024),
            _    => (long)v
        };
    }
}

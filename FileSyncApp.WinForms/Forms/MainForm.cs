using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using FileSyncApp.Core.Interfaces;
using FileSyncApp.Core.Models;
using Krypton.Toolkit;

namespace FileSyncApp.WinForms.Forms;

public partial class MainForm : KryptonForm, IFileSyncView
{
    private readonly IAuthService _authService;
    private readonly IFileStorageService _s3Service;
    private readonly IConfigurationService _configService;

    private KryptonTreeView _localTreeView = null!;
    private KryptonTreeView _s3TreeView = null!;
    private KryptonLabel _statusLabel = null!;
    private KryptonProgressBar _progressBar = null!;
    private KryptonButton _btnSync = null!;
    private KryptonButton _btnRefresh = null!;

    // Flag to prevent re-entrant calls during async operations
    private bool _isLoadingS3 = false;
    private bool _isLoadingLocal = false;

    public event EventHandler? SyncRequested;
    public event EventHandler? CancelRequested;
    public event EventHandler? RefreshRequested;

    public string StatusMessage { set => SafeSetStatus(value); }
    public int ProgressValue { set => SafeSetProgress(value); }
    public bool ProgressVisible { set => SafeSetProgressVisible(value); }

    public MainForm(IAuthService authService, IFileStorageService s3Service, IConfigurationService configService)
    {
        _authService = authService;
        _s3Service = s3Service;
        _configService = configService;
        InitializeComponent();
        InitializeTrees();

        _ = CancelRequested;
    }

    private void SafeSetStatus(string value)
    {
        if (_statusLabel.InvokeRequired)
            _statusLabel.Invoke(() => _statusLabel.Text = value);
        else
            _statusLabel.Text = value;
    }

    private void SafeSetProgress(int value)
    {
        if (_progressBar.InvokeRequired)
            _progressBar.Invoke(() => _progressBar.Value = Math.Min(value, 100));
        else
            _progressBar.Value = Math.Min(value, 100);
    }

    private void SafeSetProgressVisible(bool value)
    {
        if (_progressBar.InvokeRequired)
            _progressBar.Invoke(() => _progressBar.Visible = value);
        else
            _progressBar.Visible = value;
    }

    private void InitializeComponent()
    {
        this.Text = "FileSyncApp - Strata S3 Manager";
        this.Width = 1300;
        this.Height = 850;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.WindowState = FormWindowState.Maximized;

        var mainPanel = new KryptonPanel { Dock = DockStyle.Fill };

        var toolStrip = new KryptonPanel { Dock = DockStyle.Top, Height = 60 };
        _btnSync = new KryptonButton { Text = "Sync Now", Location = new Point(10, 10), Width = 120, Height = 40 };
        _btnSync.Click += (s, e) => SyncRequested?.Invoke(this, EventArgs.Empty);

        _btnRefresh = new KryptonButton { Text = "Refresh S3", Location = new Point(140, 10), Width = 120, Height = 40 };
        _btnRefresh.Click += (s, e) => RefreshRequested?.Invoke(this, EventArgs.Empty);

        toolStrip.Controls.AddRange(new Control[] { _btnSync, _btnRefresh });

        var splitContainer = new KryptonSplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 600
        };

        var localGroup = new KryptonGroupBox { Text = "Local Filesystem", Dock = DockStyle.Fill };
        _localTreeView = new KryptonTreeView { Dock = DockStyle.Fill };
        _localTreeView.BeforeExpand += LocalTreeView_BeforeExpand;
        localGroup.Panel.Controls.Add(_localTreeView);

        var s3Group = new KryptonGroupBox { Text = "AWS S3 Bucket", Dock = DockStyle.Fill };
        _s3TreeView = new KryptonTreeView { Dock = DockStyle.Fill };
        _s3TreeView.BeforeExpand += S3TreeView_BeforeExpand;
        s3Group.Panel.Controls.Add(_s3TreeView);

        splitContainer.Panel1.Controls.Add(localGroup);
        splitContainer.Panel2.Controls.Add(s3Group);

        var statusPanel = new KryptonPanel { Dock = DockStyle.Bottom, Height = 40 };
        _statusLabel = new KryptonLabel { Text = "Ready", Location = new Point(10, 10), Width = 600 };
        _progressBar = new KryptonProgressBar { Location = new Point(620, 10), Width = 400, Visible = false };
        statusPanel.Controls.AddRange(new Control[] { _statusLabel, _progressBar });

        mainPanel.Controls.Add(splitContainer);
        mainPanel.Controls.Add(toolStrip);
        mainPanel.Controls.Add(statusPanel);

        this.Controls.Add(mainPanel);
    }

    private void InitializeTrees()
    {
        _localTreeView.Nodes.Clear();
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
        {
            var node = new KryptonTreeNode(drive.Name) { Tag = drive.RootDirectory };
            node.Nodes.Add(new KryptonTreeNode("Loading..."));
            _localTreeView.Nodes.Add(node);
        }

        _s3TreeView.Nodes.Clear();
        var s3Root = new KryptonTreeNode("S3 Bucket Root") { Tag = "" };
        s3Root.Nodes.Add(new KryptonTreeNode("Loading..."));
        _s3TreeView.Nodes.Add(s3Root);
    }

    private void LocalTreeView_BeforeExpand(object? sender, TreeViewCancelEventArgs e)
    {
        if (_isLoadingLocal) return;
        
        if (e.Node?.Nodes.Count == 1 && e.Node.Nodes[0].Text == "Loading...")
        {
            _isLoadingLocal = true;
            e.Node.Nodes.Clear();
            var dirInfo = e.Node.Tag as DirectoryInfo;
            if (dirInfo == null && e.Node.Tag is string path) dirInfo = new DirectoryInfo(path);
            if (dirInfo == null)
            {
                _isLoadingLocal = false;
                return;
            }

            // Load synchronously but quickly for local filesystem
            try
            {
                _localTreeView.BeginUpdate();
                
                var subDirs = dirInfo.EnumerateDirectories()
                    .Take(500) // Limit to prevent hanging on huge directories
                    .ToList();
                var files = dirInfo.EnumerateFiles()
                    .Take(500)
                    .ToList();

                foreach (var dir in subDirs)
                {
                    try
                    {
                        var node = new KryptonTreeNode($"📁 {dir.Name}") { Tag = dir };
                        // Only add loading placeholder if directory likely has contents
                        try
                        {
                            if (dir.EnumerateFileSystemInfos().Any())
                                node.Nodes.Add(new KryptonTreeNode("Loading..."));
                        }
                        catch { } // Ignore access errors for placeholder check
                        e.Node.Nodes.Add(node);
                    }
                    catch (UnauthorizedAccessException) { }
                }

                foreach (var file in files)
                {
                    e.Node.Nodes.Add(new KryptonTreeNode($"📄 {file.Name}") { Tag = file });
                }
                
                _localTreeView.EndUpdate();
            }
            catch (UnauthorizedAccessException)
            {
                e.Node.Nodes.Add(new KryptonTreeNode("Access Denied"));
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
    }

    private async void S3TreeView_BeforeExpand(object? sender, TreeViewCancelEventArgs e)
    {
        if (_isLoadingS3) return;
        
        if (e.Node?.Nodes.Count == 1 && e.Node.Nodes[0].Text == "Loading...")
        {
            _isLoadingS3 = true;
            e.Node.Nodes.Clear();
            var prefix = e.Node.Tag as string ?? "";

            try
            {
                StatusMessage = "Loading S3 contents...";
                
                var currentUser = _authService.GetCurrentUser();
                if (currentUser == null)
                {
                    e.Node.Nodes.Add(new KryptonTreeNode("Not authenticated"));
                    return;
                }

                // Run S3 listing on background thread
                var items = await Task.Run(async () => 
                {
                    try
                    {
                        return await _s3Service.ListFilesAsync(currentUser.Role, prefix);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"S3 Error: {ex.Message}", ex);
                    }
                });

                // Update UI on main thread
                _s3TreeView.BeginUpdate();
                try
                {
                    foreach (var item in items.Take(500)) // Limit items for performance
                    {
                        var displayText = (item.IsDirectory ? "📁 " : "📄 ") + item.Name;
                        var node = new KryptonTreeNode(displayText) { Tag = item.Path };
                        if (item.IsDirectory)
                        {
                            node.Nodes.Add(new KryptonTreeNode("Loading..."));
                        }
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
                var bucket = _configService.GetConfiguration().AWS.BucketName;
                StatusMessage = $"Error: {ex.Message}";
                e.Node.Nodes.Add(new KryptonTreeNode($"Error: {ex.Message}"));
                
                // Show error dialog on first failure
                MessageBox.Show(
                    $"Failed to list S3 contents.\nBucket: '{bucket}'\n\nError: {ex.Message}", 
                    "S3 Error", 
                    MessageBoxButtons.OK, 
                    MessageBoxIcon.Error);
            }
            finally
            {
                _isLoadingS3 = false;
            }
        }
    }

    public void UpdateLocalTree(List<FileNode> nodes)
    {
        if (InvokeRequired)
        {
            Invoke(() => InitializeTrees());
        }
        else
        {
            InitializeTrees();
        }
    }

    public void UpdateRemoteTree(List<FileNode> nodes)
    {
        UpdateLocalTree(nodes);
    }
}

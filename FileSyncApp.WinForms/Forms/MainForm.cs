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

    private KryptonTreeView _localTreeView = null!;
    private KryptonTreeView _s3TreeView = null!;
    private KryptonLabel _statusLabel = null!;
    private KryptonProgressBar _progressBar = null!;
    private KryptonButton _btnSync = null!;
    private KryptonButton _btnRefresh = null!;

    public event EventHandler? SyncRequested;
    public event EventHandler? CancelRequested;
    public event EventHandler? RefreshRequested;

    public string StatusMessage { set => _statusLabel.Text = value; }
    public int ProgressValue { set => _progressBar.Value = value; }
    public bool ProgressVisible { set => _progressBar.Visible = value; }

    public MainForm(IAuthService authService, IFileStorageService s3Service)
    {
        _authService = authService;
        _s3Service = s3Service;
        InitializeComponent();
        InitializeTrees();

        // Suppress unused event warning if necessary, but we keep it for interface compliance
        _ = CancelRequested;
    }

    private void InitializeComponent()
    {
        this.Text = "FileSyncApp - Strata S3 Manager";
        this.Width = 1100;
        this.Height = 750;
        this.StartPosition = FormStartPosition.CenterScreen;

        var mainPanel = new KryptonPanel { Dock = DockStyle.Fill };

        var toolStrip = new KryptonPanel { Dock = DockStyle.Top, Height = 50 };
        _btnSync = new KryptonButton { Text = "Sync Now", Location = new Point(10, 10), Width = 100 };
        _btnSync.Click += (s, e) => SyncRequested?.Invoke(this, EventArgs.Empty);

        _btnRefresh = new KryptonButton { Text = "Refresh", Location = new Point(120, 10), Width = 100 };
        _btnRefresh.Click += (s, e) => RefreshRequested?.Invoke(this, EventArgs.Empty);

        toolStrip.Controls.AddRange(new Control[] { _btnSync, _btnRefresh });

        var splitContainer = new KryptonSplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 540
        };

        var localGroup = new KryptonGroupBox { Text = "Local Files", Dock = DockStyle.Fill };
        _localTreeView = new KryptonTreeView { Dock = DockStyle.Fill };
        _localTreeView.BeforeExpand += LocalTreeView_BeforeExpand;
        localGroup.Panel.Controls.Add(_localTreeView);

        var s3Group = new KryptonGroupBox { Text = "S3 Files", Dock = DockStyle.Fill };
        _s3TreeView = new KryptonTreeView { Dock = DockStyle.Fill };
        _s3TreeView.BeforeExpand += S3TreeView_BeforeExpand;
        s3Group.Panel.Controls.Add(_s3TreeView);

        splitContainer.Panel1.Controls.Add(localGroup);
        splitContainer.Panel2.Controls.Add(s3Group);

        var statusPanel = new KryptonPanel { Dock = DockStyle.Bottom, Height = 40 };
        _statusLabel = new KryptonLabel { Text = "Ready", Location = new Point(10, 10), Width = 400 };
        _progressBar = new KryptonProgressBar { Location = new Point(420, 10), Width = 300, Visible = false };
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
        var s3Root = new KryptonTreeNode("S3 Bucket") { Tag = "" };
        s3Root.Nodes.Add(new KryptonTreeNode("Loading..."));
        _s3TreeView.Nodes.Add(s3Root);
    }

    private async void LocalTreeView_BeforeExpand(object? sender, TreeViewCancelEventArgs e)
    {
        if (e.Node?.Nodes.Count == 1 && e.Node.Nodes[0].Text == "Loading...")
        {
            e.Node.Nodes.Clear();
            var dirInfo = e.Node.Tag as DirectoryInfo;
            if (dirInfo == null && e.Node.Tag is string path) dirInfo = new DirectoryInfo(path);
            if (dirInfo == null) return;

            try
            {
                var subDirs = await Task.Run(() => dirInfo.EnumerateDirectories().ToList());
                var files = await Task.Run(() => dirInfo.EnumerateFiles().ToList());

                _localTreeView.BeginUpdate();
                foreach (var dir in subDirs)
                {
                    var node = new KryptonTreeNode($"📁 {dir.Name}") { Tag = dir };
                    node.Nodes.Add(new KryptonTreeNode("Loading..."));
                    e.Node.Nodes.Add(node);
                }

                foreach (var file in files)
                {
                    e.Node.Nodes.Add(new KryptonTreeNode($"📄 {file.Name}") { Tag = file });
                }
                _localTreeView.EndUpdate();
            }
            catch (UnauthorizedAccessException) { }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }
    }

    private async void S3TreeView_BeforeExpand(object? sender, TreeViewCancelEventArgs e)
    {
        if (e.Node?.Nodes.Count == 1 && e.Node.Nodes[0].Text == "Loading...")
        {
            e.Node.Nodes.Clear();
            var prefix = e.Node.Tag as string ?? "";

            try
            {
                var currentUser = _authService.GetCurrentUser();
                if (currentUser == null) return;

                var items = await _s3Service.ListFilesAsync(currentUser.Role, prefix);

                _s3TreeView.BeginUpdate();
                foreach (var item in items)
                {
                    var displayText = (item.IsDirectory ? "📁 " : "📄 ") + item.Name;
                    var node = new KryptonTreeNode(displayText) { Tag = item.Path };
                    if (item.IsDirectory)
                    {
                        node.Nodes.Add(new KryptonTreeNode("Loading..."));
                    }
                    e.Node.Nodes.Add(node);
                }
                _s3TreeView.EndUpdate();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }
    }

    public void UpdateLocalTree(List<FileNode> nodes)
    {
        InitializeTrees();
    }

    public void UpdateRemoteTree(List<FileNode> nodes)
    {
        UpdateLocalTree(nodes);
    }
}

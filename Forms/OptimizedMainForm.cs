using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using S3FileManager.Models;
using S3FileManager.Services;

namespace S3FileManager.Forms
{
    public partial class OptimizedMainForm : Form
    {
        private readonly CognitoUser _currentUser;
        private readonly OptimizedS3Service _s3Service;
        private readonly CognitoAuthService _authService;
        private readonly FileService _fileService;

        private TreeView? _localTreeView;
        private TreeView? _s3TreeView;
        private Button? _refreshButton;
        private Button? _uploadButton;
        private Button? _downloadButton;
        private Button? _syncButton;
        private Button? _deleteButton;
        private Label? _statusLabel;
        private ProgressBar? _progressBar;
        private Label? _userInfoLabel;
        private Button? _logoutButton;
        private TextBox? _searchBox;
        private ComboBox? _filterComboBox;

        private List<S3FileItem> _s3Files = new List<S3FileItem>();
        private CancellationTokenSource? _currentOperation;
        private System.Windows.Forms.Timer? _tokenRefreshTimer;
        private bool _isVersioningEnabled = false;

        public OptimizedMainForm(CognitoUser user)
        {
            _currentUser = user;
            _s3Service = new OptimizedS3Service(user);
            _authService = new CognitoAuthService();
            _fileService = new FileService();

            InitializeComponent();
            InitializeAsync();
        }

        private void InitializeComponent()
        {
            this.Text = $"Strata S3 Manager - {_currentUser.Username} ({_currentUser.Role})";
            this.Size = new Size(1200, 700);
            this.StartPosition = FormStartPosition.CenterScreen;

            // Create main layout
            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 2
            };

            // Add row styles
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); // Header
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Content
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // Status bar

            // Add column styles
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            // Header panel
            var headerPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(240, 240, 240)
            };

            _userInfoLabel = new Label
            {
                Text = $"User: {_currentUser.Username} | Role: {_currentUser.Role} | " +
                       (_currentUser.IsOfflineMode ? "Mode: Offline" : "Mode: Online"),
                Location = new Point(10, 15),
                Size = new Size(400, 20),
                Font = new Font("Arial", 10, FontStyle.Bold)
            };

            _searchBox = new TextBox
            {
                Location = new Point(420, 12),
                Size = new Size(200, 25),
                PlaceholderText = "Search files..."
            };
            _searchBox.TextChanged += SearchBox_TextChanged;

            _filterComboBox = new ComboBox
            {
                Location = new Point(630, 12),
                Size = new Size(150, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _filterComboBox.Items.AddRange(new[] { "All Files", "Documents", "Images", "Archives" });
            _filterComboBox.SelectedIndex = 0;
            _filterComboBox.SelectedIndexChanged += FilterComboBox_SelectedIndexChanged;

            _refreshButton = new Button
            {
                Text = "🔄 Refresh",
                Location = new Point(790, 10),
                Size = new Size(90, 30)
            };
            _refreshButton.Click += RefreshButton_Click;

            _logoutButton = new Button
            {
                Text = "Logout",
                Location = new Point(1090, 10),
                Size = new Size(80, 30)
            };
            _logoutButton.Click += LogoutButton_Click;

            headerPanel.Controls.AddRange(new Control[]
            {
                _userInfoLabel, _searchBox, _filterComboBox, _refreshButton, _logoutButton
            });

            // Local files panel
            var localPanel = new GroupBox
            {
                Text = "Local Files",
                Dock = DockStyle.Fill,
                Margin = new Padding(5)
            };

            _localTreeView = new TreeView
            {
                Dock = DockStyle.Fill,
                CheckBoxes = true,
                ImageList = CreateImageList()
            };
            _localTreeView.BeforeExpand += LocalTreeView_BeforeExpand;
            _localTreeView.AfterCheck += TreeView_AfterCheck;

            var localToolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40
            };

            var browseButton = new Button
            {
                Text = "📁 Browse",
                Location = new Point(5, 5),
                Size = new Size(80, 30)
            };
            browseButton.Click += BrowseButton_Click;

            localToolbar.Controls.Add(browseButton);
            localPanel.Controls.Add(_localTreeView);
            localPanel.Controls.Add(localToolbar);

            // S3 files panel
            var s3Panel = new GroupBox
            {
                Text = "S3 Files",
                Dock = DockStyle.Fill,
                Margin = new Padding(5)
            };

            _s3TreeView = new TreeView
            {
                Dock = DockStyle.Fill,
                CheckBoxes = true,
                ImageList = CreateImageList()
            };
            _s3TreeView.BeforeExpand += S3TreeView_BeforeExpand;
            _s3TreeView.AfterCheck += TreeView_AfterCheck;

            s3Panel.Controls.Add(_s3TreeView);

            // Action buttons panel
            var actionPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(250, 250, 250)
            };

            _uploadButton = new Button
            {
                Text = "⬆️ Upload",
                Location = new Point(10, 10),
                Size = new Size(100, 35),
                Enabled = false
            };
            _uploadButton.Click += UploadButton_Click;

            _downloadButton = new Button
            {
                Text = "⬇️ Download",
                Location = new Point(120, 10),
                Size = new Size(100, 35),
                Enabled = false
            };
            _downloadButton.Click += DownloadButton_Click;

            _syncButton = new Button
            {
                Text = "🔄 Sync",
                Location = new Point(230, 10),
                Size = new Size(100, 35)
            };
            _syncButton.Click += SyncButton_Click;

            _deleteButton = new Button
            {
                Text = "🗑️ Delete",
                Location = new Point(340, 10),
                Size = new Size(100, 35),
                Enabled = false
            };
            _deleteButton.Click += DeleteButton_Click;

            var cancelButton = new Button
            {
                Text = "❌ Cancel",
                Location = new Point(450, 10),
                Size = new Size(100, 35),
                Visible = false
            };
            cancelButton.Click += (s, e) => _currentOperation?.Cancel();

            actionPanel.Controls.AddRange(new Control[]
            {
                _uploadButton, _downloadButton, _syncButton, _deleteButton, cancelButton
            });

            // Status bar
            var statusPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(230, 230, 230)
            };

            _statusLabel = new Label
            {
                Text = "Ready",
                Location = new Point(10, 5),
                Size = new Size(400, 20)
            };

            _progressBar = new ProgressBar
            {
                Location = new Point(420, 3),
                Size = new Size(300, 23),
                Visible = false
            };

            statusPanel.Controls.AddRange(new Control[] { _statusLabel, _progressBar });

            // Add all panels to main layout
            mainPanel.Controls.Add(headerPanel, 0, 0);
            mainPanel.SetColumnSpan(headerPanel, 2);

            mainPanel.Controls.Add(localPanel, 0, 1);
            mainPanel.Controls.Add(s3Panel, 1, 1);

            mainPanel.Controls.Add(actionPanel, 0, 2);
            mainPanel.Controls.Add(statusPanel, 1, 2);

            this.Controls.Add(mainPanel);

            // Setup token refresh timer
            if (!_currentUser.IsOfflineMode)
            {
                _tokenRefreshTimer = new System.Windows.Forms.Timer();
                _tokenRefreshTimer.Interval = 30 * 60 * 1000; // 30 minutes
                _tokenRefreshTimer.Tick += async (s, e) => await RefreshTokens();
                _tokenRefreshTimer.Start();
            }
        }

        private async void InitializeAsync()
        {
            await RefreshS3Files();
            _isVersioningEnabled = await _s3Service.IsVersioningEnabledAsync();
        }

        private ImageList CreateImageList()
        {
            var imageList = new ImageList();
            imageList.Images.Add("folder", SystemIcons.Shield.ToBitmap()); // Placeholder
            // Use the default folder icon from the system
            var folderIcon = Icon.ExtractAssociatedIcon(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
            imageList.Images.Add("folder", folderIcon != null ? folderIcon.ToBitmap() : SystemIcons.WinLogo.ToBitmap());
            // Use the default file icon (e.g., .txt file)
            var tempFile = Path.Combine(Path.GetTempPath(), "tempfile.txt");
            File.WriteAllText(tempFile, ""); // Ensure file exists
            var fileIcon = Icon.ExtractAssociatedIcon(tempFile);
            imageList.Images.Add("file", fileIcon != null ? fileIcon.ToBitmap() : SystemIcons.Application.ToBitmap());
            File.Delete(tempFile);
            return imageList;
        }

        private async Task RefreshS3Files()
        {
            try
            {
                _currentOperation = new CancellationTokenSource();
                UpdateStatus("Refreshing S3 files...");
                ShowProgress(true);

                _s3Files = await _s3Service.ListFilesAsync(
                    _currentUser.Role,
                    "",
                    _currentOperation.Token,
                    true); // Use delimited listing for lazy loading

                UpdateS3TreeView();
                UpdateStatus($"Loaded {_s3Files.Count} root items from S3");
            }
            catch (OperationCanceledException)
            {
                UpdateStatus("Operation cancelled");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error: {ex.Message}");
                MessageBox.Show($"Failed to refresh S3 files: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                ShowProgress(false);
                _currentOperation = null;
            }
        }

        private void UpdateS3TreeView()
        {
            if (_s3TreeView == null) return;

            _s3TreeView.BeginUpdate();
            _s3TreeView.Nodes.Clear();

            foreach (var file in _s3Files.OrderBy(f => f.IsDirectory ? 0 : 1).ThenBy(f => f.Key))
            {
                AddS3Node(_s3TreeView.Nodes, file);
            }

            _s3TreeView.EndUpdate();
        }

        private void AddS3Node(TreeNodeCollection parentNodes, S3FileItem file)
        {
            var displayName = file.Key.TrimEnd('/').Split('/').Last();
            string displayText = file.IsDirectory
                ? $"📁 {displayName}"
                : $"📄 {displayName} ({FormatFileSize(file.Size)})";

            var node = new TreeNode(displayText)
            {
                Tag = file,
                ImageIndex = file.IsDirectory ? 0 : 1,
                SelectedImageIndex = file.IsDirectory ? 0 : 1
            };

            if (file.IsDirectory)
            {
                node.Nodes.Add("Loading...");
            }

            parentNodes.Add(node);
        }

        private void LocalTreeView_BeforeExpand(object? sender, TreeViewCancelEventArgs e)
        {
            // Lazy load local directory contents
            if (e.Node?.Tag is string path && e.Node.Nodes.Count == 1 &&
                e.Node.Nodes[0].Text == "Loading...")
            {
                e.Node.Nodes.Clear();
                LoadLocalDirectory(e.Node, path);
            }
        }

        private async void S3TreeView_BeforeExpand(object? sender, TreeViewCancelEventArgs e)
        {
            if (e.Node?.Tag is S3FileItem item && item.IsDirectory && e.Node.Nodes.Count > 0 && e.Node.Nodes[0].Text == "Loading...")
            {
                e.Node.Nodes.Clear();
                try
                {
                    var childFiles = await _s3Service.ListFilesAsync(_currentUser.Role, item.Key, _currentOperation?.Token ?? CancellationToken.None, true);
                    foreach (var file in childFiles)
                    {
                        AddS3Node(e.Node.Nodes, file);
                    }
                }
                catch (Exception ex)
                {
                    e.Node.Nodes.Add($"Error: {ex.Message}");
                }
            }
        }

        private void TreeView_AfterCheck(object? sender, TreeViewEventArgs e)
        {
            // Update button states based on selections
            UpdateButtonStates();
        }

        private void UpdateButtonStates()
        {
            var hasLocalSelection = GetCheckedNodes(_localTreeView).Any();
            var hasS3Selection = GetCheckedNodes(_s3TreeView).Any();

            _uploadButton!.Enabled = hasLocalSelection;
            _downloadButton!.Enabled = hasS3Selection;
            _deleteButton!.Enabled = hasS3Selection && _currentUser.Role == UserRole.Administrator;
        }

        private IEnumerable<TreeNode> GetCheckedNodes(TreeView? treeView)
        {
            if (treeView == null) yield break;

            foreach (TreeNode node in treeView.Nodes)
            {
                foreach (var checkedNode in GetCheckedNodesRecursive(node))
                {
                    yield return checkedNode;
                }
            }
        }

        private IEnumerable<TreeNode> GetCheckedNodesRecursive(TreeNode node)
        {
            if (node.Checked)
                yield return node;

            foreach (TreeNode child in node.Nodes)
            {
                foreach (var checkedNode in GetCheckedNodesRecursive(child))
                {
                    yield return checkedNode;
                }
            }
        }

        private void BrowseButton_Click(object? sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select folder to browse";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    LoadLocalDirectory(dialog.SelectedPath);
                }
            }
        }

        private void LoadLocalDirectory(string path)
        {
            if (_localTreeView == null) return;

            _localTreeView.Nodes.Clear();
            var rootNode = new TreeNode(Path.GetFileName(path) ?? path)
            {
                Tag = path
            };

            LoadLocalDirectory(rootNode, path);
            _localTreeView.Nodes.Add(rootNode);
            rootNode.Expand();
        }

        private void LoadLocalDirectory(TreeNode parentNode, string path)
        {
            try
            {
                // Add directories
                foreach (var dir in Directory.GetDirectories(path))
                {
                    var dirNode = new TreeNode($"📁 {Path.GetFileName(dir)}")
                    {
                        Tag = dir,
                        ImageIndex = 0,
                        SelectedImageIndex = 0
                    };

                    // Add dummy node for lazy loading
                    if (Directory.GetDirectories(dir).Length != 0 || Directory.GetFiles(dir).Length != 0)
                    {
                        dirNode.Nodes.Add(new TreeNode("Loading..."));
                    }

                    parentNode.Nodes.Add(dirNode);
                }

                // Add files
                foreach (var file in Directory.GetFiles(path))
                {
                    var fileInfo = new FileInfo(file);
                    var fileNode = new TreeNode(
                        $"📄 {Path.GetFileName(file)} ({FormatFileSize(fileInfo.Length)})")
                    {
                        Tag = file,
                        ImageIndex = 1,
                        SelectedImageIndex = 1
                    };

                    parentNode.Nodes.Add(fileNode);
                }
            }
            catch (Exception ex)
            {
                parentNode.Nodes.Add(new TreeNode($"Error: {ex.Message}"));
            }
        }

        private async void UploadButton_Click(object? sender, EventArgs e)
        {
            var checkedNodes = GetCheckedNodes(_localTreeView).ToList();
            if (!checkedNodes.Any()) return;

            if (!_isVersioningEnabled)
            {
                var result = MessageBox.Show(
                    "Warning: S3 Versioning is not enabled on this bucket. Uploading files will overwrite existing files permanently. Do you want to continue?",
                    "Versioning Not Enabled",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.No)
                {
                    return;
                }
            }

            // Get target S3 path
            var targetPath = "";
            var selectedS3Node = _s3TreeView?.SelectedNode;
            if (selectedS3Node?.Tag is S3FileItem item && item.IsDirectory)
            {
                targetPath = item.Key;
            }

            _currentOperation = new CancellationTokenSource();
            var progress = new Progress<int>(percent =>
            {
                _progressBar!.Value = percent;
                UpdateStatus($"Uploading... {percent}%");
            });

            try
            {
                ShowProgress(true);
                UpdateStatus("Starting upload...");

                foreach (var node in checkedNodes)
                {
                    if (node.Tag is string path)
                    {
                        if (File.Exists(path))
                        {
                            var key = Path.Combine(targetPath, Path.GetFileName(path)).Replace('\\', '/');
                            await _s3Service.UploadFileAsync(
                                path, key,
                                new List<UserRole> { _currentUser.Role },
                                null, _currentOperation.Token);
                        }
                        else if (Directory.Exists(path))
                        {
                            await _s3Service.UploadDirectoryAsync(
                                path, targetPath,
                                new List<UserRole> { _currentUser.Role },
                                progress, _currentOperation.Token);
                        }
                    }
                }

                UpdateStatus("Upload completed");
                await RefreshS3Files();
            }
            catch (OperationCanceledException)
            {
                UpdateStatus("Upload cancelled");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Upload failed: {ex.Message}");
                MessageBox.Show($"Upload failed: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                ShowProgress(false);
                _currentOperation = null;
            }
        }

        private async void DownloadButton_Click(object? sender, EventArgs e)
        {
            var checkedNodes = GetCheckedNodes(_s3TreeView).ToList();
            if (!checkedNodes.Any()) return;

            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select download location";

                if (dialog.ShowDialog() != DialogResult.OK) return;

                _currentOperation = new CancellationTokenSource();
                var progress = new Progress<double>(percent =>
                {
                    _progressBar!.Value = (int)percent;
                    UpdateStatus($"Downloading... {percent:F1}%");
                });

                try
                {
                    ShowProgress(true);
                    UpdateStatus("Starting download...");

                    foreach (var node in checkedNodes)
                    {
                        if (node.Tag is S3FileItem item)
                        {
                            await _s3Service.DownloadFileAsync(
                                item.Key, dialog.SelectedPath,
                                progress, _currentOperation.Token);
                        }
                    }

                    UpdateStatus("Download completed");
                }
                catch (OperationCanceledException)
                {
                    UpdateStatus("Download cancelled");
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Download failed: {ex.Message}");
                    MessageBox.Show($"Download failed: {ex.Message}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    ShowProgress(false);
                    _currentOperation = null;
                }
            }
        }

        private async void SyncButton_Click(object? sender, EventArgs e)
        {
            using (var directionForm = new SyncDirectionForm())
            {
                if (directionForm.ShowDialog() != DialogResult.OK) return;

                var selectedDirection = directionForm.SelectedDirection;
                if (selectedDirection == SyncDirection.None) return;

                if (selectedDirection == SyncDirection.LocalToS3 && !_isVersioningEnabled)
                {
                    var result = MessageBox.Show(
                        "Warning: S3 Versioning is not enabled on this bucket. Syncing to S3 will overwrite existing files permanently. Do you want to continue?",
                        "Versioning Not Enabled",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    if (result == DialogResult.No)
                    {
                        return;
                    }
                }

                string localPath;
                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.Description = "Select local folder to sync";
                    if (dialog.ShowDialog() != DialogResult.OK) return;
                    localPath = dialog.SelectedPath;
                }

                // For now, we sync with the root of the bucket. A more advanced implementation
                // could allow selecting an S3 folder.
                string s3Prefix = "";

                _currentOperation = new CancellationTokenSource();
                var progress = new Progress<SyncProgress>(p =>
                {
                    if (_progressBar != null) _progressBar.Value = (int)p.PercentComplete;
                    UpdateStatus(p.Status);
                });

                try
                {
                    ShowProgress(true);
                    SyncResult result;
                    string action = "";

                    if (selectedDirection == SyncDirection.S3ToLocal)
                    {
                        action = "Download";
                        result = await _s3Service.SyncS3ToLocalAsync(
                            localPath, s3Prefix,
                            _currentUser.Role, progress, _currentOperation.Token);
                    }
                    else // LocalToS3
                    {
                        action = "Upload";
                        result = await _s3Service.SyncLocalToS3Async(
                            localPath, s3Prefix,
                            _currentUser.Role, progress, _currentOperation.Token);
                    }

                    var message = $"Sync ({action}) completed:\n\n" +
                                 (result.UploadedCount > 0 ? $"Uploaded: {result.UploadedCount} files\n" : "") +
                                 (result.DownloadedCount > 0 ? $"Downloaded: {result.DownloadedCount} files\n" : "") +
                                 $"Skipped: {result.SkippedCount} files\n" +
                                 $"Errors: {result.Errors.Count}";

                    if (result.DeletedFiles.Any())
                    {
                        message += $"\n\nExtra local files found: {result.DeletedFiles.Count}\n" +
                                  "These files are not present in the S3 bucket. Would you like to delete them?";

                        if (MessageBox.Show(message, "Sync Complete",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                        {
                            foreach (var file in result.DeletedFiles)
                            {
                                try { File.Delete(file); } catch { }
                            }
                        }
                    }
                    else
                    {
                        MessageBox.Show(message, "Sync Complete",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }

                    UpdateStatus("Sync completed");
                    await RefreshS3Files();
                }
                catch (OperationCanceledException)
                {
                    UpdateStatus("Sync cancelled");
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Sync failed: {ex.Message}");
                    MessageBox.Show($"Sync failed: {ex.Message}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    ShowProgress(false);
                    _currentOperation = null;
                }
            }
        }

        private async void DeleteButton_Click(object? sender, EventArgs e)
        {
            var checkedNodes = GetCheckedNodes(_s3TreeView).ToList();
            if (!checkedNodes.Any()) return;

            if (MessageBox.Show(
                $"Are you sure you want to delete {checkedNodes.Count} selected item(s)?",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            _currentOperation = new CancellationTokenSource();

            try
            {
                ShowProgress(true);
                UpdateStatus("Deleting files...");

                foreach (var node in checkedNodes)
                {
                    if (node.Tag is S3FileItem item)
                    {
                        await _s3Service.DeleteFileAsync(item.Key, _currentOperation.Token);
                    }
                }

                UpdateStatus("Delete completed");
                await RefreshS3Files();
            }
            catch (OperationCanceledException)
            {
                UpdateStatus("Delete cancelled");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Delete failed: {ex.Message}");
                MessageBox.Show($"Delete failed: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                ShowProgress(false);
                _currentOperation = null;
            }
        }

        private void SearchBox_TextChanged(object? sender, EventArgs e)
        {
            // Implement file search/filtering
        }

        private void FilterComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            // Implement file type filtering
        }

        private async Task RefreshTokens()
        {
            try
            {
                await _authService.RefreshTokensAsync();
                UpdateStatus("Authentication tokens refreshed");
            }
            catch
            {
                UpdateStatus("Failed to refresh tokens - please re-login");
            }
        }

        private async void LogoutButton_Click(object? sender, EventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to logout?", "Confirm Logout",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                await _authService.SignOutAsync();
                this.Close();
                Application.Restart();
            }
        }

        private void UpdateStatus(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => _statusLabel!.Text = message));
            }
            else
            {
                _statusLabel!.Text = message;
            }
        }

        private void ShowProgress(bool show)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() =>
                {
                    _progressBar!.Visible = show;
                    if (!show) _progressBar!.Value = 0;
                }));
            }
            else
            {
                _progressBar!.Visible = show;
                if (!show) _progressBar!.Value = 0;
            }
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;

            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:0.##} {sizes[order]}";
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            _tokenRefreshTimer?.Stop();
            _tokenRefreshTimer?.Dispose();
            _currentOperation?.Cancel();
            _s3Service?.Dispose();
            _authService?.Dispose();
        }
    }
}
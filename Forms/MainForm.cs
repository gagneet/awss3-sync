// Forms/MainForm.cs - Optimized Version
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using S3FileManager.Models;
using S3FileManager.Services;

namespace S3FileManager
{
    public partial class MainForm : Form
    {
        private readonly User _currentUser;
        private readonly S3Service _s3Service;
        private readonly FileService _fileService;
        private string _selectedLocalPath = "";
        private List<LocalFileItem> _localFiles = new List<LocalFileItem>();
        private List<S3FileItem> _s3Files = new List<S3FileItem>();

        // Performance optimization: Cache and selection tracking
        private readonly Dictionary<string, bool> _s3CheckedItems = new Dictionary<string, bool>();
        private readonly Dictionary<string, bool> _localCheckedItems = new Dictionary<string, bool>();
        private bool _isUpdatingTree = false;
        private TreeNode? _lastSelectedS3Node = null;
        private TreeNode? _lastSelectedLocalNode = null;

        public MainForm(User currentUser)
        {
            _currentUser = currentUser;
            _s3Service = new S3Service();
            _fileService = new FileService();
            InitializeComponent();
            SetupUserInterface();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.AutoScaleDimensions = new SizeF(8F, 16F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(1400, 900);
            this.Text = $"AWS S3 File Manager - {_currentUser.Username} ({_currentUser.Role})";
            this.StartPosition = FormStartPosition.CenterScreen;

            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(10)
            };
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            var leftPanel = CreateLocalPanel();
            mainPanel.Controls.Add(leftPanel, 0, 0);

            var rightPanel = CreateS3Panel();
            mainPanel.Controls.Add(rightPanel, 1, 0);

            this.Controls.Add(mainPanel);
            this.ResumeLayout(false);
        }

        private Panel CreateLocalPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill };

            var headerLabel = new Label
            {
                Text = "Local Files",
                Font = new Font("Arial", 12, FontStyle.Bold),
                Location = new Point(10, 10),
                Size = new Size(200, 25)
            };

            var pathLabel = new Label
            {
                Text = "Selected Path: None",
                Location = new Point(10, 40),
                Size = new Size(650, 20),
                Name = "pathLabel"
            };

            var localTreeView = new TreeView
            {
                Location = new Point(10, 70),
                Size = new Size(650, 450),
                Name = "localTreeView",
                CheckBoxes = true,
                ShowLines = true,
                ShowPlusMinus = true,
                ShowRootLines = true,
                HideSelection = false,
                // Performance optimizations
                BeginUpdate = true,
                Scrollable = true
            };

            // Event handlers for performance and selection persistence
            localTreeView.BeforeExpand += LocalTreeView_BeforeExpand;
            localTreeView.AfterCheck += LocalTreeView_AfterCheck;
            localTreeView.BeforeCheck += LocalTreeView_BeforeCheck;

            var buttonY = 530;
            var browseButton = new Button
            {
                Text = "Browse Files/Folders",
                Location = new Point(10, buttonY),
                Size = new Size(130, 30)
            };
            browseButton.Click += BrowseButton_Click;

            var uploadButton = new Button
            {
                Text = "Upload Selected",
                Location = new Point(150, buttonY),
                Size = new Size(120, 30),
                Enabled = _currentUser.Role == UserRole.Administrator || _currentUser.Role == UserRole.Executive
            };
            uploadButton.Click += UploadButton_Click;

            var syncButton = new Button
            {
                Text = "Sync Folder",
                Location = new Point(280, buttonY),
                Size = new Size(100, 30),
                Enabled = _currentUser.Role == UserRole.Administrator
            };
            syncButton.Click += SyncButton_Click;

            // Selection counter
            var selectionLabel = new Label
            {
                Text = "Selected: 0 items",
                Location = new Point(400, buttonY + 5),
                Size = new Size(150, 20),
                Name = "localSelectionLabel",
                ForeColor = Color.Blue
            };

            panel.Controls.AddRange(new Control[]
            {
                headerLabel, pathLabel, localTreeView,
                browseButton, uploadButton, syncButton, selectionLabel
            });

            return panel;
        }

        private Panel CreateS3Panel()
        {
            var panel = new Panel { Dock = DockStyle.Fill };

            var headerLabel = new Label
            {
                Text = "S3 Bucket Files",
                Font = new Font("Arial", 12, FontStyle.Bold),
                Location = new Point(10, 10),
                Size = new Size(200, 25)
            };

            var bucketLabel = new Label
            {
                Text = "Bucket: Loading...",
                Location = new Point(10, 40),
                Size = new Size(650, 20),
                Name = "bucketLabel"
            };

            var s3TreeView = new TreeView
            {
                Location = new Point(10, 70),
                Size = new Size(650, 450),
                Name = "s3TreeView",
                CheckBoxes = true,
                ShowLines = true,
                ShowPlusMinus = true,
                ShowRootLines = true,
                HideSelection = false,
                // Performance optimizations
                BeginUpdate = true,
                Scrollable = true,
                // Virtual mode for large datasets
                VirtualMode = false // We'll implement custom virtualization
            };

            // Event handlers for performance and selection persistence
            s3TreeView.AfterCheck += S3TreeView_AfterCheck;
            s3TreeView.BeforeCheck += S3TreeView_BeforeCheck;
            s3TreeView.BeforeExpand += S3TreeView_BeforeExpand;
            s3TreeView.AfterExpand += S3TreeView_AfterExpand;

            var buttonY = 530;
            var listButton = new Button
            {
                Text = "List S3 Files",
                Location = new Point(10, buttonY),
                Size = new Size(100, 30)
            };
            listButton.Click += ListS3Button_Click;

            var downloadButton = new Button
            {
                Text = "Download Selected",
                Location = new Point(120, buttonY),
                Size = new Size(130, 30),
                Enabled = _currentUser.Role != UserRole.User
            };
            downloadButton.Click += DownloadButton_Click;

            var deleteButton = new Button
            {
                Text = "Delete Selected",
                Location = new Point(260, buttonY),
                Size = new Size(120, 30),
                Enabled = _currentUser.Role == UserRole.Administrator
            };
            deleteButton.Click += DeleteButton_Click;

            var permissionsButton = new Button
            {
                Text = "Manage Permissions",
                Location = new Point(390, buttonY),
                Size = new Size(130, 30),
                Enabled = _currentUser.Role == UserRole.Administrator
            };
            permissionsButton.Click += ManagePermissionsButton_Click;

            var refreshButton = new Button
            {
                Text = "Refresh",
                Location = new Point(530, buttonY),
                Size = new Size(80, 30)
            };
            refreshButton.Click += RefreshS3Button_Click;

            // Selection counter and search
            var selectionLabel = new Label
            {
                Text = "Selected: 0 items",
                Location = new Point(10, buttonY + 35),
                Size = new Size(150, 20),
                Name = "s3SelectionLabel",
                ForeColor = Color.Blue
            };

            var searchLabel = new Label
            {
                Text = "Search:",
                Location = new Point(200, buttonY + 35),
                Size = new Size(50, 20)
            };

            var searchTextBox = new TextBox
            {
                Location = new Point(250, buttonY + 33),
                Size = new Size(150, 20),
                Name = "searchTextBox",
                PlaceholderText = "Filter files..."
            };
            searchTextBox.TextChanged += SearchTextBox_TextChanged;

            var clearSearchButton = new Button
            {
                Text = "Clear",
                Location = new Point(410, buttonY + 31),
                Size = new Size(50, 24)
            };
            clearSearchButton.Click += ClearSearchButton_Click;

            panel.Controls.AddRange(new Control[]
            {
                headerLabel, bucketLabel, s3TreeView,
                listButton, downloadButton, deleteButton, permissionsButton, refreshButton,
                selectionLabel, searchLabel, searchTextBox, clearSearchButton
            });

            return panel;
        }

        private void SetupUserInterface()
        {
            try
            {
                var config = ConfigurationService.GetConfiguration();
                var bucketLabel = this.Controls.Find("bucketLabel", true).FirstOrDefault() as Label;
                if (bucketLabel != null)
                    bucketLabel.Text = $"Bucket: {config.AWS.BucketName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error setting up interface: {ex.Message}", "Setup Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            // Load S3 files on startup asynchronously
            Task.Run(async () =>
            {
                try
                {
                    await LoadS3FilesAsync();
                }
                catch (Exception ex)
                {
                    this.Invoke(new Action(() =>
                    {
                        MessageBox.Show($"Error loading S3 files on startup: {ex.Message}", "Startup Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }));
                }
            });
        }

        #region Selection Persistence and Performance Optimization

        private void LocalTreeView_BeforeCheck(object? sender, TreeViewCancelEventArgs e)
        {
            if (_isUpdatingTree || e.Node?.Tag == null) return;

            var item = e.Node.Tag as LocalFileItem;
            if (item != null)
            {
                _localCheckedItems[item.FullPath] = !e.Node.Checked;
            }
        }

        private void LocalTreeView_AfterCheck(object? sender, TreeViewEventArgs e)
        {
            if (_isUpdatingTree || e.Node?.Tag == null) return;

            var item = e.Node.Tag as LocalFileItem;
            if (item != null)
            {
                _localCheckedItems[item.FullPath] = e.Node.Checked;
                UpdateLocalSelectionCount();

                // Auto-check/uncheck children for folders
                if (item.IsDirectory)
                {
                    _isUpdatingTree = true;
                    SetChildrenChecked(e.Node, e.Node.Checked);
                    _isUpdatingTree = false;
                }
            }
        }

        private void S3TreeView_BeforeCheck(object? sender, TreeViewCancelEventArgs e)
        {
            if (_isUpdatingTree || e.Node?.Tag == null) return;

            var item = e.Node.Tag as S3FileItem;
            if (item != null)
            {
                _s3CheckedItems[item.Key] = !e.Node.Checked;
            }
        }

        private void S3TreeView_AfterCheck(object? sender, TreeViewEventArgs e)
        {
            if (_isUpdatingTree || e.Node?.Tag == null) return;

            var item = e.Node.Tag as S3FileItem;
            if (item != null)
            {
                _s3CheckedItems[item.Key] = e.Node.Checked;
                UpdateS3SelectionCount();

                // Auto-check/uncheck children for folders
                if (item.IsDirectory)
                {
                    _isUpdatingTree = true;
                    SetChildrenChecked(e.Node, e.Node.Checked);
                    _isUpdatingTree = false;
                }
            }
        }

        private void S3TreeView_BeforeExpand(object? sender, TreeViewCancelEventArgs e)
        {
            // Performance: Only expand if not already loaded
            if (e.Node != null && e.Node.Nodes.Count == 1 && e.Node.Nodes[0].Text == "Loading...")
            {
                // This would be implemented for lazy loading of S3 subfolders if needed
            }
        }

        private void S3TreeView_AfterExpand(object? sender, TreeViewEventArgs e)
        {
            // Restore checked states after expansion
            if (e.Node != null)
            {
                RestoreCheckedStates(e.Node.Nodes, _s3CheckedItems, true);
            }
        }

        private void SetChildrenChecked(TreeNode parentNode, bool isChecked)
        {
            foreach (TreeNode childNode in parentNode.Nodes)
            {
                childNode.Checked = isChecked;

                // Update the tracking dictionary
                if (childNode.Tag is LocalFileItem localItem)
                {
                    _localCheckedItems[localItem.FullPath] = isChecked;
                }
                else if (childNode.Tag is S3FileItem s3Item)
                {
                    _s3CheckedItems[s3Item.Key] = isChecked;
                }

                // Recursively check children
                if (childNode.Nodes.Count > 0)
                {
                    SetChildrenChecked(childNode, isChecked);
                }
            }
        }

        private void RestoreCheckedStates(TreeNodeCollection nodes, Dictionary<string, bool> checkedItems, bool isS3)
        {
            foreach (TreeNode node in nodes)
            {
                string key = "";
                if (isS3 && node.Tag is S3FileItem s3Item)
                {
                    key = s3Item.Key;
                }
                else if (!isS3 && node.Tag is LocalFileItem localItem)
                {
                    key = localItem.FullPath;
                }

                if (!string.IsNullOrEmpty(key) && checkedItems.ContainsKey(key))
                {
                    node.Checked = checkedItems[key];
                }

                // Recursively restore for children
                if (node.Nodes.Count > 0)
                {
                    RestoreCheckedStates(node.Nodes, checkedItems, isS3);
                }
            }
        }

        private void UpdateLocalSelectionCount()
        {
            var count = _localCheckedItems.Values.Count(v => v);
            var label = this.Controls.Find("localSelectionLabel", true).FirstOrDefault() as Label;
            if (label != null)
            {
                label.Text = $"Selected: {count} items";
            }
        }

        private void UpdateS3SelectionCount()
        {
            var count = _s3CheckedItems.Values.Count(v => v);
            var label = this.Controls.Find("s3SelectionLabel", true).FirstOrDefault() as Label;
            if (label != null)
            {
                label.Text = $"Selected: {count} items";
            }
        }

        #endregion

        #region Search and Filter

        private void SearchTextBox_TextChanged(object? sender, EventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            var searchTerm = textBox.Text.ToLowerInvariant();
            var s3TreeView = this.Controls.Find("s3TreeView", true).FirstOrDefault() as TreeView;
            if (s3TreeView == null) return;

            // Perform search with debouncing
            Task.Delay(300).ContinueWith(_ =>
            {
                if (textBox.Text.ToLowerInvariant() == searchTerm) // Only proceed if text hasn't changed
                {
                    this.Invoke(new Action(() => FilterS3Tree(searchTerm)));
                }
            });
        }

        private void ClearSearchButton_Click(object? sender, EventArgs e)
        {
            var searchTextBox = this.Controls.Find("searchTextBox", true).FirstOrDefault() as TextBox;
            if (searchTextBox != null)
            {
                searchTextBox.Text = "";
                FilterS3Tree("");
            }
        }

        private void FilterS3Tree(string searchTerm)
        {
            var s3TreeView = this.Controls.Find("s3TreeView", true).FirstOrDefault() as TreeView;
            if (s3TreeView == null) return;

            _isUpdatingTree = true;
            s3TreeView.BeginUpdate();

            try
            {
                if (string.IsNullOrEmpty(searchTerm))
                {
                    // Show all nodes
                    ShowAllNodes(s3TreeView.Nodes);
                }
                else
                {
                    // Hide nodes that don't match search
                    FilterNodes(s3TreeView.Nodes, searchTerm);
                }

                // Restore checked states after filtering
                RestoreCheckedStates(s3TreeView.Nodes, _s3CheckedItems, true);
            }
            finally
            {
                s3TreeView.EndUpdate();
                _isUpdatingTree = false;
            }
        }

        private bool FilterNodes(TreeNodeCollection nodes, string searchTerm)
        {
            bool hasVisibleChildren = false;

            foreach (TreeNode node in nodes)
            {
                bool nodeMatches = node.Text.ToLowerInvariant().Contains(searchTerm);
                bool hasMatchingChildren = node.Nodes.Count > 0 && FilterNodes(node.Nodes, searchTerm);

                bool shouldShow = nodeMatches || hasMatchingChildren;

                // Show/hide the node
                if (shouldShow)
                {
                    node.BackColor = nodeMatches ? Color.LightYellow : Color.Transparent;
                    node.ForeColor = Color.Black;
                    hasVisibleChildren = true;

                    if (hasMatchingChildren)
                    {
                        node.Expand();
                    }
                }
                else
                {
                    node.BackColor = Color.Transparent;
                    node.ForeColor = Color.LightGray;
                }
            }

            return hasVisibleChildren;
        }

        private void ShowAllNodes(TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes)
            {
                node.BackColor = Color.Transparent;
                node.ForeColor = Color.Black;

                if (node.Nodes.Count > 0)
                {
                    ShowAllNodes(node.Nodes);
                }
            }
        }

        #endregion

        #region Local File Operations

        private void BrowseButton_Click(object? sender, EventArgs e)
        {
            using (var folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Select a folder to browse";

                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    _selectedLocalPath = folderDialog.SelectedPath;
                    LoadLocalFiles(_selectedLocalPath);

                    var pathLabel = this.Controls.Find("pathLabel", true).FirstOrDefault() as Label;
                    if (pathLabel != null)
                        pathLabel.Text = $"Selected Path: {_selectedLocalPath}";
                }
            }
        }

        private void LoadLocalFiles(string path)
        {
            try
            {
                var localTreeView = this.Controls.Find("localTreeView", true).FirstOrDefault() as TreeView;
                if (localTreeView == null) return;

                _isUpdatingTree = true;
                localTreeView.BeginUpdate();

                // Clear previous selections for this path
                _localCheckedItems.Clear();

                localTreeView.Nodes.Clear();

                // Create root node for the selected folder
                var rootNode = new TreeNode(Path.GetFileName(path) ?? path)
                {
                    Tag = new LocalFileItem
                    {
                        Name = Path.GetFileName(path) ?? path,
                        FullPath = path,
                        IsDirectory = true,
                        Size = 0
                    }
                };

                // Load immediate children
                LoadLocalDirectoryNodes(rootNode, path);

                localTreeView.Nodes.Add(rootNode);
                rootNode.Expand();

                localTreeView.EndUpdate();
                _isUpdatingTree = false;

                UpdateLocalSelectionCount();
            }
            catch (Exception ex)
            {
                _isUpdatingTree = false;
                MessageBox.Show($"Error loading local files: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadLocalDirectoryNodes(TreeNode parentNode, string directoryPath)
        {
            try
            {
                // Add directories first
                foreach (string dir in Directory.GetDirectories(directoryPath))
                {
                    var dirInfo = new DirectoryInfo(dir);
                    var dirNode = new TreeNode($"📁 {dirInfo.Name}")
                    {
                        Tag = new LocalFileItem
                        {
                            Name = dirInfo.Name,
                            FullPath = dir,
                            IsDirectory = true,
                            Size = 0
                        }
                    };

                    // Add a dummy node to show the expand button if directory is not empty
                    try
                    {
                        if (Directory.GetDirectories(dir).Length > 0 || Directory.GetFiles(dir).Length > 0)
                        {
                            dirNode.Nodes.Add(new TreeNode("Loading..."));
                        }
                    }
                    catch
                    {
                        // If we can't access the directory, just add it without children
                    }

                    parentNode.Nodes.Add(dirNode);
                }

                // Add files
                foreach (string file in Directory.GetFiles(directoryPath))
                {
                    var fileInfo = new FileInfo(file);
                    var fileNode = new TreeNode($"📄 {fileInfo.Name} ({_fileService.FormatFileSize(fileInfo.Length)})")
                    {
                        Tag = new LocalFileItem
                        {
                            Name = fileInfo.Name,
                            FullPath = file,
                            IsDirectory = false,
                            Size = fileInfo.Length
                        }
                    };

                    parentNode.Nodes.Add(fileNode);
                }
            }
            catch (Exception ex)
            {
                parentNode.Nodes.Add(new TreeNode($"Error: {ex.Message}"));
            }
        }

        private void LocalTreeView_BeforeExpand(object? sender, TreeViewCancelEventArgs e)
        {
            var node = e.Node;
            if (node?.Tag is LocalFileItem item && item.IsDirectory)
            {
                // Check if this is a dummy expansion (has only one "Loading..." node)
                if (node.Nodes.Count == 1 && node.Nodes[0].Text == "Loading...")
                {
                    _isUpdatingTree = true;

                    var treeView = sender as TreeView;
                    treeView?.BeginUpdate();

                    node.Nodes.Clear();
                    LoadLocalDirectoryNodes(node, item.FullPath);

                    // Restore checked states after expansion
                    RestoreCheckedStates(node.Nodes, _localCheckedItems, false);

                    treeView?.EndUpdate();
                    _isUpdatingTree = false;
                }
            }
        }

        private List<LocalFileItem> GetCheckedLocalItems()
        {
            var items = new List<LocalFileItem>();

            foreach (var kvp in _localCheckedItems)
            {
                if (kvp.Value) // If checked
                {
                    var item = _localFiles.FirstOrDefault(f => f.FullPath == kvp.Key);
                    if (item != null)
                    {
                        items.Add(item);
                    }
                    else
                    {
                        // Create item from path if not in cache
                        try
                        {
                            var info = new FileInfo(kvp.Key);
                            if (info.Exists)
                            {
                                items.Add(new LocalFileItem
                                {
                                    Name = info.Name,
                                    FullPath = kvp.Key,
                                    IsDirectory = false,
                                    Size = info.Length
                                });
                            }
                            else
                            {
                                var dirInfo = new DirectoryInfo(kvp.Key);
                                if (dirInfo.Exists)
                                {
                                    items.Add(new LocalFileItem
                                    {
                                        Name = dirInfo.Name,
                                        FullPath = kvp.Key,
                                        IsDirectory = true,
                                        Size = 0
                                    });
                                }
                            }
                        }
                        catch
                        {
                            // Skip invalid paths
                        }
                    }
                }
            }

            return items;
        }

        #endregion

        #region Upload Operations

        private async void UploadButton_Click(object? sender, EventArgs e)
        {
            if (_currentUser.Role == UserRole.User)
            {
                MessageBox.Show("Users cannot upload files.", "Access Denied",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedItems = GetCheckedLocalItems();

            if (selectedItems.Count == 0)
            {
                MessageBox.Show("Please select files or folders to upload.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // For Executives, show folder selection for upload destination
            string uploadPrefix = "";
            List<UserRole> defaultRoles;

            if (_currentUser.Role == UserRole.Executive)
            {
                var folderForm = new ExecutiveUploadFolderForm();
                if (folderForm.ShowDialog() != DialogResult.OK)
                    return;

                uploadPrefix = folderForm.SelectedFolder;
                defaultRoles = new List<UserRole> { UserRole.Executive, UserRole.Administrator };
            }
            else
            {
                // Show role selection dialog for Administrators
                var roleForm = new RoleSelectionForm();
                if (roleForm.ShowDialog() != DialogResult.OK)
                    return;

                defaultRoles = roleForm.SelectedRoles;
            }

            try
            {
                var progressForm = new ProgressForm("Uploading files...");
                progressForm.Show();

                for (int i = 0; i < selectedItems.Count; i++)
                {
                    var item = selectedItems[i];
                    progressForm.UpdateMessage($"Uploading: {item.Name} ({i + 1}/{selectedItems.Count})");

                    string uploadKey = string.IsNullOrEmpty(uploadPrefix) ? item.Name : $"{uploadPrefix}/{item.Name}";

                    if (item.IsDirectory)
                    {
                        await _s3Service.UploadDirectoryAsync(item.FullPath, uploadKey, defaultRoles);
                    }
                    else
                    {
                        await _s3Service.UploadFileAsync(item.FullPath, uploadKey, defaultRoles);
                    }
                }

                progressForm.Close();
                MessageBox.Show($"Upload completed successfully! ({selectedItems.Count} items)", "Success",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                await LoadS3FilesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error uploading files: {ex.Message}", "Upload Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void SyncButton_Click(object? sender, EventArgs e)
        {
            if (_currentUser.Role != UserRole.Administrator)
            {
                MessageBox.Show("Only administrators can sync folders.", "Access Denied",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrEmpty(_selectedLocalPath))
            {
                MessageBox.Show("Please browse and select a local folder first.", "No Folder Selected",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var roleForm = new RoleSelectionForm();
            if (roleForm.ShowDialog() != DialogResult.OK)
                return;

            try
            {
                var progressForm = new ProgressForm("Syncing folder...");
                progressForm.Show();

                string folderName = Path.GetFileName(_selectedLocalPath);
                await _s3Service.UploadDirectoryAsync(_selectedLocalPath, folderName, roleForm.SelectedRoles);

                progressForm.Close();
                MessageBox.Show("Sync completed successfully!", "Success",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                await LoadS3FilesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during sync: {ex.Message}", "Sync Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region S3 Operations - Optimized

        private async void ListS3Button_Click(object? sender, EventArgs e)
        {
            await LoadS3FilesAsync();
        }

        private async void RefreshS3Button_Click(object? sender, EventArgs e)
        {
            // Clear search before refresh
            var searchTextBox = this.Controls.Find("searchTextBox", true).FirstOrDefault() as TextBox;
            if (searchTextBox != null)
                searchTextBox.Text = "";

            await LoadS3FilesAsync();
        }

        private async Task LoadS3FilesAsync()
        {
            try
            {
                // Show loading indicator
                var bucketLabel = this.Controls.Find("bucketLabel", true).FirstOrDefault() as Label;
                if (bucketLabel != null)
                {
                    this.Invoke(new Action(() => bucketLabel.Text = "Loading..."));
                }

                _s3Files = await _s3Service.ListFilesAsync(_currentUser.Role);

                // Update UI on main thread
                this.Invoke(new Action(() => UpdateS3TreeViewOptimized()));
            }
            catch (Exception ex)
            {
                this.Invoke(new Action(() =>
                {
                    MessageBox.Show($"Error loading S3 files: {ex.Message}", "S3 Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);

                    var bucketLabel = this.Controls.Find("bucketLabel", true).FirstOrDefault() as Label;
                    if (bucketLabel != null)
                        bucketLabel.Text = "Error loading bucket";
                }));
            }
        }

        private void UpdateS3TreeViewOptimized()
        {
            var s3TreeView = this.Controls.Find("s3TreeView", true).FirstOrDefault() as TreeView;
            if (s3TreeView == null) return;

            _isUpdatingTree = true;
            s3TreeView.BeginUpdate();

            try
            {
                // Store current expanded states and scroll position
                var expandedNodes = new HashSet<string>();
                var scrollPosition = GetTreeViewScrollPosition(s3TreeView);
                StoreExpandedStates(s3TreeView.Nodes, expandedNodes);

                s3TreeView.Nodes.Clear();

                // Build tree structure efficiently
                var nodeCache = new Dictionary<string, TreeNode>();

                // Sort files for better performance
                var sortedFiles = _s3Files.OrderBy(f => f.Key).ToList();

                foreach (var item in sortedFiles)
                {
                    AddS3ItemToTreeOptimized(s3TreeView, nodeCache, item);
                }

                // Restore expanded states
                RestoreExpandedStates(s3TreeView.Nodes, expandedNodes);

                // Restore checked states
                RestoreCheckedStates(s3TreeView.Nodes, _s3CheckedItems, true);

                // Restore scroll position
                SetTreeViewScrollPosition(s3TreeView, scrollPosition);

                // Update UI labels
                var bucketLabel = this.Controls.Find("bucketLabel", true).FirstOrDefault() as Label;
                if (bucketLabel != null)
                {
                    var config = ConfigurationService.GetConfiguration();
                    bucketLabel.Text = $"Bucket: {config.AWS.BucketName} ({_s3Files.Count} items visible)";
                }

                UpdateS3SelectionCount();
            }
            finally
            {
                s3TreeView.EndUpdate();
                _isUpdatingTree = false;
            }
        }

        private void AddS3ItemToTreeOptimized(TreeView treeView, Dictionary<string, TreeNode> nodeCache, S3FileItem item)
        {
            var pathParts = item.Key.Split('/');
            TreeNodeCollection currentNodes = treeView.Nodes;
            string currentPath = "";

            for (int i = 0; i < pathParts.Length; i++)
            {
                var part = pathParts[i];
                if (string.IsNullOrEmpty(part)) continue;

                currentPath += (i > 0 ? "/" : "") + part;
                bool isLastPart = i == pathParts.Length - 1;
                bool isFile = isLastPart && !item.IsDirectory;

                // Check cache first for performance
                TreeNode? existingNode = null;
                if (nodeCache.ContainsKey(currentPath))
                {
                    existingNode = nodeCache[currentPath];
                }
                else
                {
                    // Look for existing node in current level only
                    foreach (TreeNode node in currentNodes)
                    {
                        if (node.Tag is S3FileItem nodeItem &&
                            (nodeItem.Key == currentPath || nodeItem.Key == currentPath + "/"))
                        {
                            existingNode = node;
                            nodeCache[currentPath] = node;
                            break;
                        }
                    }
                }

                if (existingNode == null)
                {
                    // Create new node
                    var nodeItem = new S3FileItem
                    {
                        Key = isFile ? currentPath : currentPath + "/",
                        Size = isFile ? item.Size : 0,
                        LastModified = item.LastModified,
                        AccessRoles = item.AccessRoles
                    };

                    string displayText = isFile ?
                        $"📄 {part} ({_fileService.FormatFileSize(item.Size)})" :
                        $"📁 {part}";

                    var newNode = new TreeNode(displayText)
                    {
                        Tag = nodeItem,
                        Name = currentPath // Set name for easier searching
                    };

                    currentNodes.Add(newNode);
                    nodeCache[currentPath] = newNode;
                    existingNode = newNode;
                }

                if (!isLastPart)
                {
                    currentNodes = existingNode.Nodes;
                }
            }
        }

        private void StoreExpandedStates(TreeNodeCollection nodes, HashSet<string> expandedNodes)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.IsExpanded && node.Tag is S3FileItem item)
                {
                    expandedNodes.Add(item.Key);
                }

                if (node.Nodes.Count > 0)
                {
                    StoreExpandedStates(node.Nodes, expandedNodes);
                }
            }
        }

        private void RestoreExpandedStates(TreeNodeCollection nodes, HashSet<string> expandedNodes)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Tag is S3FileItem item && expandedNodes.Contains(item.Key))
                {
                    node.Expand();
                }

                if (node.Nodes.Count > 0)
                {
                    RestoreExpandedStates(node.Nodes, expandedNodes);
                }
            }
        }

        private int GetTreeViewScrollPosition(TreeView treeView)
        {
            try
            {
                return SendMessage(treeView.Handle, TVM_GETSCROLLPOS, 0, 0);
            }
            catch
            {
                return 0;
            }
        }

        private void SetTreeViewScrollPosition(TreeView treeView, int position)
        {
            try
            {
                SendMessage(treeView.Handle, TVM_SETSCROLLPOS, 0, position);
            }
            catch
            {
                // Ignore if unable to set scroll position
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        private const int TVM_SETSCROLLPOS = 0x1100 + 63;
        private const int TVM_GETSCROLLPOS = 0x1100 + 62;

        private List<S3FileItem> GetCheckedS3Items()
        {
            var items = new List<S3FileItem>();

            foreach (var kvp in _s3CheckedItems)
            {
                if (kvp.Value) // If checked
                {
                    var item = _s3Files.FirstOrDefault(f => f.Key == kvp.Key);
                    if (item != null)
                    {
                        items.Add(item);
                    }
                }
            }

            return items;
        }

        private async void DownloadButton_Click(object? sender, EventArgs e)
        {
            var selectedItems = GetCheckedS3Items();

            if (selectedItems.Count == 0)
            {
                MessageBox.Show("Please select files to download.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Check permissions
            if (_currentUser.Role == UserRole.User)
            {
                MessageBox.Show("Users cannot download files.", "Access Denied",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Select download destination";

                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var progressForm = new ProgressForm("Downloading files...");
                        progressForm.Show();

                        var filesToDownload = selectedItems.Where(i => !i.IsDirectory).ToList();

                        for (int i = 0; i < filesToDownload.Count; i++)
                        {
                            var item = filesToDownload[i];
                            progressForm.UpdateMessage($"Downloading: {item.Key} ({i + 1}/{filesToDownload.Count})");
                            await _s3Service.DownloadFileAsync(item.Key, folderDialog.SelectedPath);
                        }

                        progressForm.Close();
                        MessageBox.Show($"Downloaded {filesToDownload.Count} files successfully!", "Success",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error downloading files: {ex.Message}", "Download Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private async void DeleteButton_Click(object? sender, EventArgs e)
        {
            if (_currentUser.Role != UserRole.Administrator)
            {
                MessageBox.Show("Only administrators can delete files.", "Access Denied",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedItems = GetCheckedS3Items();

            if (selectedItems.Count == 0)
            {
                MessageBox.Show("Please select files to delete.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Enhanced confirmation dialog
            var confirmForm = new DeleteConfirmationForm(selectedItems);
            if (confirmForm.ShowDialog() != DialogResult.OK)
                return;

            try
            {
                var progressForm = new ProgressForm("Deleting files...");
                progressForm.Show();

                for (int i = 0; i < selectedItems.Count; i++)
                {
                    var item = selectedItems[i];
                    progressForm.UpdateMessage($"Deleting: {item.Key} ({i + 1}/{selectedItems.Count})");
                    await _s3Service.DeleteFileAsync(item.Key);

                    // Remove from checked items
                    _s3CheckedItems.Remove(item.Key);
                }

                progressForm.Close();
                MessageBox.Show($"Deleted {selectedItems.Count} items successfully!", "Success",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                await LoadS3FilesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting files: {ex.Message}", "Delete Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void ManagePermissionsButton_Click(object? sender, EventArgs e)
        {
            if (_currentUser.Role != UserRole.Administrator)
            {
                MessageBox.Show("Only administrators can manage permissions.", "Access Denied",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedItems = GetCheckedS3Items();

            if (selectedItems.Count == 0)
            {
                MessageBox.Show("Please select files or folders to manage permissions.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var permissionForm = new PermissionManagementForm(selectedItems);
            if (permissionForm.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var progressForm = new ProgressForm("Updating permissions...");
                    progressForm.Show();

                    var metadataService = new MetadataService();

                    for (int i = 0; i < selectedItems.Count; i++)
                    {
                        var item = selectedItems[i];
                        progressForm.UpdateMessage($"Updating: {item.Key} ({i + 1}/{selectedItems.Count})");

                        await metadataService.SetFileAccessRolesAsync(item.Key, permissionForm.SelectedRoles);

                        // If it's a folder, apply recursively
                        if (item.IsDirectory)
                        {
                            await ApplyPermissionsRecursively(item.Key, permissionForm.SelectedRoles, metadataService);
                        }
                    }

                    progressForm.Close();
                    MessageBox.Show($"Updated permissions for {selectedItems.Count} items successfully!", "Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);

                    await LoadS3FilesAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error updating permissions: {ex.Message}", "Permission Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async Task ApplyPermissionsRecursively(string folderKey, List<UserRole> roles, MetadataService metadataService)
        {
            // Get all files that start with this folder path
            var childItems = _s3Files.Where(f => f.Key.StartsWith(folderKey) && f.Key != folderKey).ToList();

            foreach (var child in childItems)
            {
                await metadataService.SetFileAccessRolesAsync(child.Key, roles);
            }
        }

        #endregion

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _s3Service?.Dispose();
            base.OnFormClosed(e);
        }
    }

    #region Supporting Forms (same as before but optimized)

    // Enhanced Delete Confirmation Form
    public class DeleteConfirmationForm : Form
    {
        private readonly List<S3FileItem> _itemsToDelete;

        public DeleteConfirmationForm(List<S3FileItem> itemsToDelete)
        {
            _itemsToDelete = itemsToDelete;
            InitializeComponent();
            PopulateItems();
        }

        private void InitializeComponent()
        {
            this.Size = new Size(500, 400);
            this.Text = "Confirm Deletion - DANGER ZONE";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(255, 248, 248);

            var warningLabel = new Label
            {
                Text = "⚠️ WARNING: This action cannot be undone!",
                Font = new Font("Arial", 12, FontStyle.Bold),
                ForeColor = Color.Red,
                Location = new Point(20, 20),
                Size = new Size(450, 30),
                TextAlign = ContentAlignment.MiddleCenter
            };

            var itemCountLabel = new Label
            {
                Text = $"You are about to permanently delete {_itemsToDelete.Count} item(s):",
                Font = new Font("Arial", 10, FontStyle.Bold),
                Location = new Point(20, 60),
                Size = new Size(450, 25)
            };

            var itemsListBox = new ListBox
            {
                Location = new Point(20, 90),
                Size = new Size(450, 180),
                Name = "itemsListBox"
            };

            var confirmationLabel = new Label
            {
                Text = "Type 'DELETE' to confirm:",
                Font = new Font("Arial", 10, FontStyle.Bold),
                Location = new Point(20, 280),
                Size = new Size(200, 25)
            };

            var confirmationTextBox = new TextBox
            {
                Location = new Point(220, 278),
                Size = new Size(100, 25),
                Name = "confirmationTextBox"
            };
            confirmationTextBox.TextChanged += ConfirmationTextBox_TextChanged;

            var deleteButton = new Button
            {
                Text = "DELETE PERMANENTLY",
                Location = new Point(250, 320),
                Size = new Size(150, 35),
                BackColor = Color.Red,
                ForeColor = Color.White,
                Font = new Font("Arial", 10, FontStyle.Bold),
                DialogResult = DialogResult.OK,
                Enabled = false,
                Name = "deleteButton"
            };

            var cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(410, 320),
                Size = new Size(70, 35),
                DialogResult = DialogResult.Cancel
            };

            this.Controls.AddRange(new Control[]
            {
                warningLabel, itemCountLabel, itemsListBox,
                confirmationLabel, confirmationTextBox, deleteButton, cancelButton
            });

            this.AcceptButton = deleteButton;
            this.CancelButton = cancelButton;
        }

        private void PopulateItems()
        {
            var itemsListBox = this.Controls.Find("itemsListBox", false)[0] as ListBox;
            if (itemsListBox == null) return;

            foreach (var item in _itemsToDelete)
            {
                string itemType = item.IsDirectory ? "[FOLDER]" : "[FILE]";
                itemsListBox.Items.Add($"{itemType} {item.Key}");
            }
        }

        private void ConfirmationTextBox_TextChanged(object? sender, EventArgs e)
        {
            var textBox = sender as TextBox;
            var deleteButton = this.Controls.Find("deleteButton", false)[0] as Button;

            if (textBox != null && deleteButton != null)
            {
                deleteButton.Enabled = textBox.Text == "DELETE";
            }
        }
    }

    // Permission Management Form
    public class PermissionManagementForm : Form
    {
        private readonly List<S3FileItem> _selectedItems;
        public List<UserRole> SelectedRoles { get; private set; } = new List<UserRole>();

        public PermissionManagementForm(List<S3FileItem> selectedItems)
        {
            _selectedItems = selectedItems;
            InitializeComponent();
            PopulateItems();
            LoadCurrentPermissions();
        }

        private void InitializeComponent()
        {
            this.Size = new Size(600, 500);
            this.Text = "Manage File/Folder Permissions";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            var titleLabel = new Label
            {
                Text = "Permission Management",
                Font = new Font("Arial", 14, FontStyle.Bold),
                Location = new Point(20, 20),
                Size = new Size(550, 30),
                TextAlign = ContentAlignment.MiddleCenter
            };

            var itemsLabel = new Label
            {
                Text = $"Managing permissions for {_selectedItems.Count} item(s):",
                Font = new Font("Arial", 10, FontStyle.Bold),
                Location = new Point(20, 60),
                Size = new Size(550, 25)
            };

            var itemsListBox = new ListBox
            {
                Location = new Point(20, 90),
                Size = new Size(550, 120),
                Name = "itemsListBox"
            };

            var permissionsGroupBox = new GroupBox
            {
                Text = "Access Permissions",
                Location = new Point(20, 220),
                Size = new Size(550, 180),
                Font = new Font("Arial", 10, FontStyle.Bold)
            };

            var userCheckBox = new CheckBox
            {
                Text = "👥 User Role - Basic users can view and access these files",
                Location = new Point(20, 30),
                Size = new Size(500, 25),
                Name = "userCheckBox"
            };

            var executiveCheckBox = new CheckBox
            {
                Text = "👔 Executive Role - Can download and upload to specific folders",
                Location = new Point(20, 60),
                Size = new Size(500, 25),
                Name = "executiveCheckBox"
            };

            var adminCheckBox = new CheckBox
            {
                Text = "🔐 Administrator Role - Full access (always enabled)",
                Location = new Point(20, 90),
                Size = new Size(500, 25),
                Name = "adminCheckBox",
                Checked = true,
                Enabled = false
            };

            var noteLabel = new Label
            {
                Text = "Note: For folders, permissions will be applied recursively to all contents.\n" +
                       "Executive upload permissions only apply to specific folders as configured.",
                ForeColor = Color.Gray,
                Location = new Point(20, 120),
                Size = new Size(500, 40)
            };

            permissionsGroupBox.Controls.AddRange(new Control[]
            {
                userCheckBox, executiveCheckBox, adminCheckBox, noteLabel
            });

            var applyButton = new Button
            {
                Text = "Apply Permissions",
                Location = new Point(380, 420),
                Size = new Size(120, 35),
                DialogResult = DialogResult.OK,
                Font = new Font("Arial", 10, FontStyle.Bold),
                BackColor = Color.LightGreen
            };
            applyButton.Click += ApplyButton_Click;

            var cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(510, 420),
                Size = new Size(70, 35),
                DialogResult = DialogResult.Cancel
            };

            this.Controls.AddRange(new Control[]
            {
                titleLabel, itemsLabel, itemsListBox, permissionsGroupBox,
                applyButton, cancelButton
            });

            this.AcceptButton = applyButton;
            this.CancelButton = cancelButton;
        }

        private void PopulateItems()
        {
            var itemsListBox = this.Controls.Find("itemsListBox", false)[0] as ListBox;
            if (itemsListBox == null) return;

            foreach (var item in _selectedItems)
            {
                string itemType = item.IsDirectory ? "📁 [FOLDER]" : "📄 [FILE]";
                string permissions = string.Join(", ", item.AccessRoles.Select(r => r.ToString()));
                itemsListBox.Items.Add($"{itemType} {item.Key} (Current: {permissions})");
            }
        }

        private async void LoadCurrentPermissions()
        {
            if (_selectedItems.Count == 1)
            {
                var item = _selectedItems[0];
                var metadataService = new MetadataService();

                try
                {
                    var currentRoles = await metadataService.GetFileAccessRolesAsync(item.Key);

                    var userCheckBox = this.Controls.Find("userCheckBox", true)[0] as CheckBox;
                    var executiveCheckBox = this.Controls.Find("executiveCheckBox", true)[0] as CheckBox;

                    if (userCheckBox != null)
                        userCheckBox.Checked = currentRoles.Contains(UserRole.User);

                    if (executiveCheckBox != null)
                        executiveCheckBox.Checked = currentRoles.Contains(UserRole.Executive);
                }
                catch
                {
                    // If we can't load current permissions, start with default
                }
            }
        }

        private void ApplyButton_Click(object? sender, EventArgs e)
        {
            SelectedRoles.Clear();

            var userCheckBox = this.Controls.Find("userCheckBox", true)[0] as CheckBox;
            var executiveCheckBox = this.Controls.Find("executiveCheckBox", true)[0] as CheckBox;

            if (userCheckBox?.Checked == true)
                SelectedRoles.Add(UserRole.User);

            if (executiveCheckBox?.Checked == true)
                SelectedRoles.Add(UserRole.Executive);

            SelectedRoles.Add(UserRole.Administrator);
        }
    }

    // Executive Upload Folder Selection Form
    public class ExecutiveUploadFolderForm : Form
    {
        public string SelectedFolder { get; private set; } = "";
        private readonly string[] _allowedFolders = { "executive-committee", "reports", "shared-documents" };

        public ExecutiveUploadFolderForm()
        {
            InitializeComponent();
            PopulateFolders();
        }

        private void InitializeComponent()
        {
            this.Size = new Size(400, 300);
            this.Text = "Select Upload Destination";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            var titleLabel = new Label
            {
                Text = "Executive Upload Permissions",
                Font = new Font("Arial", 12, FontStyle.Bold),
                Location = new Point(20, 20),
                Size = new Size(350, 30),
                TextAlign = ContentAlignment.MiddleCenter
            };

            var instructionLabel = new Label
            {
                Text = "As an Executive, you can upload files to the following folders:",
                Location = new Point(20, 60),
                Size = new Size(350, 40)
            };

            var foldersListBox = new ListBox
            {
                Location = new Point(20, 110),
                Size = new Size(350, 100),
                Name = "foldersListBox"
            };

            var noteLabel = new Label
            {
                Text = "Note: Files uploaded will be accessible to Executives and Administrators.",
                ForeColor = Color.Gray,
                Location = new Point(20, 220),
                Size = new Size(350, 30)
            };

            var uploadButton = new Button
            {
                Text = "Upload Here",
                Location = new Point(230, 250),
                Size = new Size(90, 30),
                DialogResult = DialogResult.OK,
                Enabled = false,
                Name = "uploadButton"
            };
            uploadButton.Click += UploadButton_Click;

            var cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(330, 250),
                Size = new Size(70, 30),
                DialogResult = DialogResult.Cancel
            };

            foldersListBox.SelectedIndexChanged += FoldersListBox_SelectedIndexChanged;

            this.Controls.AddRange(new Control[]
            {
                titleLabel, instructionLabel, foldersListBox, noteLabel,
                uploadButton, cancelButton
            });

            this.AcceptButton = uploadButton;
            this.CancelButton = cancelButton;
        }

        private void PopulateFolders()
        {
            var foldersListBox = this.Controls.Find("foldersListBox", false)[0] as ListBox;
            if (foldersListBox == null) return;

            foreach (var folder in _allowedFolders)
            {
                foldersListBox.Items.Add($"📁 {folder}");
            }
        }

        private void FoldersListBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            var listBox = sender as ListBox;
            var uploadButton = this.Controls.Find("uploadButton", false)[0] as Button;

            if (listBox != null && uploadButton != null)
            {
                uploadButton.Enabled = listBox.SelectedIndex >= 0;
            }
        }

        private void UploadButton_Click(object? sender, EventArgs e)
        {
            var foldersListBox = this.Controls.Find("foldersListBox", false)[0] as ListBox;
            if (foldersListBox != null && foldersListBox.SelectedIndex >= 0)
            {
                SelectedFolder = _allowedFolders[foldersListBox.SelectedIndex];
            }
        }
    }

    #endregion
}
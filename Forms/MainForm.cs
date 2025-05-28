// Forms/MainForm.cs
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
                HideSelection = false
            };
            localTreeView.BeforeExpand += LocalTreeView_BeforeExpand;

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
                Enabled = _currentUser.Role == UserRole.Administrator
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

            panel.Controls.AddRange(new Control[]
            {
                headerLabel, pathLabel, localTreeView,
                browseButton, uploadButton, syncButton
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
                HideSelection = false
            };

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

            var refreshButton = new Button
            {
                Text = "Refresh",
                Location = new Point(390, buttonY),
                Size = new Size(80, 30)
            };
            refreshButton.Click += RefreshS3Button_Click;

            panel.Controls.AddRange(new Control[]
            {
                headerLabel, bucketLabel, s3TreeView,
                listButton, downloadButton, deleteButton, refreshButton
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

            // Load S3 files on startup
            Task.Run(async () =>
            {
                try
                {
                    await LoadS3Files();
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
            }
            catch (Exception ex)
            {
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
                    node.Nodes.Clear();
                    LoadLocalDirectoryNodes(node, item.FullPath);
                }
            }
        }

        private List<LocalFileItem> GetCheckedLocalItems(TreeNodeCollection nodes)
        {
            var items = new List<LocalFileItem>();

            foreach (TreeNode node in nodes)
            {
                if (node.Checked && node.Tag is LocalFileItem item)
                {
                    items.Add(item);
                }

                // Recursively check child nodes
                items.AddRange(GetCheckedLocalItems(node.Nodes));
            }

            return items;
        }

        #endregion

        #region Upload Operations

        private async void UploadButton_Click(object? sender, EventArgs e)
        {
            if (_currentUser.Role != UserRole.Administrator)
            {
                MessageBox.Show("Only administrators can upload files.", "Access Denied",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var localTreeView = this.Controls.Find("localTreeView", true).FirstOrDefault() as TreeView;
            if (localTreeView == null) return;

            var selectedItems = GetCheckedLocalItems(localTreeView.Nodes);

            if (selectedItems.Count == 0)
            {
                MessageBox.Show("Please select files or folders to upload.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Show role selection dialog
            var roleForm = new RoleSelectionForm();
            if (roleForm.ShowDialog() != DialogResult.OK)
                return;

            try
            {
                var progressForm = new ProgressForm("Uploading files...");
                progressForm.Show();

                foreach (var item in selectedItems)
                {
                    progressForm.UpdateMessage($"Uploading: {item.Name}");

                    if (item.IsDirectory)
                    {
                        await _s3Service.UploadDirectoryAsync(item.FullPath, item.Name, roleForm.SelectedRoles);
                    }
                    else
                    {
                        await _s3Service.UploadFileAsync(item.FullPath, item.Name, roleForm.SelectedRoles);
                    }
                }

                progressForm.Close();
                MessageBox.Show("Upload completed successfully!", "Success",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                await LoadS3Files();
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

                await LoadS3Files();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during sync: {ex.Message}", "Sync Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region S3 Operations

        private async void ListS3Button_Click(object? sender, EventArgs e)
        {
            await LoadS3Files();
        }

        private async void RefreshS3Button_Click(object? sender, EventArgs e)
        {
            await LoadS3Files();
        }

        private async Task LoadS3Files()
        {
            try
            {
                _s3Files = await _s3Service.ListFilesAsync(_currentUser.Role);

                // Update UI on main thread
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() => UpdateS3TreeView()));
                }
                else
                {
                    UpdateS3TreeView();
                }
            }
            catch (Exception ex)
            {
                var action = new Action(() =>
                {
                    MessageBox.Show($"Error loading S3 files: {ex.Message}", "S3 Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                });

                if (this.InvokeRequired)
                    this.Invoke(action);
                else
                    action();
            }
        }

        private void UpdateS3TreeView()
        {
            var s3TreeView = this.Controls.Find("s3TreeView", true).FirstOrDefault() as TreeView;
            if (s3TreeView == null) return;

            s3TreeView.Nodes.Clear();

            // Create hierarchical structure from flat S3 file list
            var rootNodes = new Dictionary<string, TreeNode>();

            foreach (var item in _s3Files)
            {
                AddS3ItemToTree(s3TreeView, rootNodes, item);
            }

            var bucketLabel = this.Controls.Find("bucketLabel", true).FirstOrDefault() as Label;
            if (bucketLabel != null)
            {
                var config = ConfigurationService.GetConfiguration();
                bucketLabel.Text = $"Bucket: {config.AWS.BucketName} ({_s3Files.Count} items visible)";
            }
        }

        private void AddS3ItemToTree(TreeView treeView, Dictionary<string, TreeNode> rootNodes, S3FileItem item)
        {
            var pathParts = item.Key.Split('/');
            TreeNodeCollection currentNodes = treeView.Nodes;
            TreeNode? parentNode = null;
            string currentPath = "";

            for (int i = 0; i < pathParts.Length; i++)
            {
                var part = pathParts[i];
                if (string.IsNullOrEmpty(part)) continue;

                currentPath += (i > 0 ? "/" : "") + part;
                bool isLastPart = i == pathParts.Length - 1;
                bool isFile = isLastPart && !item.IsDirectory;

                // Look for existing node
                TreeNode? existingNode = null;
                foreach (TreeNode node in currentNodes)
                {
                    if (node.Tag is S3FileItem nodeItem &&
                        (nodeItem.Key == currentPath || nodeItem.Key == currentPath + "/"))
                    {
                        existingNode = node;
                        break;
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
                        Tag = nodeItem
                    };

                    currentNodes.Add(newNode);
                    existingNode = newNode;
                }

                if (!isLastPart)
                {
                    currentNodes = existingNode.Nodes;
                    parentNode = existingNode;
                }
            }
        }

        private List<S3FileItem> GetCheckedS3Items(TreeNodeCollection nodes)
        {
            var items = new List<S3FileItem>();

            foreach (TreeNode node in nodes)
            {
                if (node.Checked && node.Tag is S3FileItem item)
                {
                    items.Add(item);
                }

                // Recursively check child nodes
                items.AddRange(GetCheckedS3Items(node.Nodes));
            }

            return items;
        }

        private async void DownloadButton_Click(object? sender, EventArgs e)
        {
            var s3TreeView = this.Controls.Find("s3TreeView", true).FirstOrDefault() as TreeView;
            if (s3TreeView == null) return;

            var selectedItems = GetCheckedS3Items(s3TreeView.Nodes);

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

            var s3TreeView = this.Controls.Find("s3TreeView", true).FirstOrDefault() as TreeView;
            if (s3TreeView == null) return;

            var selectedItems = GetCheckedS3Items(s3TreeView.Nodes);

            if (selectedItems.Count == 0)
            {
                MessageBox.Show("Please select files to delete.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var result = MessageBox.Show($"Are you sure you want to delete {selectedItems.Count} item(s)?",
                "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
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
                }

                progressForm.Close();
                MessageBox.Show($"Deleted {selectedItems.Count} items successfully!", "Success",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                await LoadS3Files();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting files: {ex.Message}", "Delete Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _s3Service?.Dispose();
            base.OnFormClosed(e);
        }
    }
}
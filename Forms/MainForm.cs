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

            panel.Controls.AddRange(new Control[]
            {
                headerLabel, bucketLabel, s3TreeView,
                listButton, downloadButton, deleteButton, permissionsButton, refreshButton
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
            if (_currentUser.Role == UserRole.User)
            {
                MessageBox.Show("Users cannot upload files.", "Access Denied",
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

                foreach (var item in selectedItems)
                {
                    progressForm.UpdateMessage($"Uploading: {item.Name}");

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

        private async void ManagePermissionsButton_Click(object? sender, EventArgs e)
        {
            if (_currentUser.Role != UserRole.Administrator)
            {
                MessageBox.Show("Only administrators can manage permissions.", "Access Denied",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var s3TreeView = this.Controls.Find("s3TreeView", true).FirstOrDefault() as TreeView;
            if (s3TreeView == null) return;

            var selectedItems = GetCheckedS3Items(s3TreeView.Nodes);

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

                    await LoadS3Files();
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
            this.BackColor = Color.FromArgb(255, 248, 248); // Light red background

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

            // Administrator is always included
            SelectedRoles.Add(UserRole.Administrator);
        }
    }
}
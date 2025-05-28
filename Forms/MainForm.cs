using System;
using System.Collections.Generic;
using System.Drawing;
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

            var localListBox = new CheckedListBox
            {
                Location = new Point(10, 70),
                Size = new Size(650, 450),
                Name = "localListBox",
                CheckOnClick = true
            };

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
                headerLabel, pathLabel, localListBox,
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
                Text = $"Bucket: {ConfigurationService.GetConfiguration().AWS.BucketName}",
                Location = new Point(10, 40),
                Size = new Size(650, 20),
                Name = "bucketLabel"
            };

            var s3ListBox = new CheckedListBox
            {
                Location = new Point(10, 70),
                Size = new Size(650, 450),
                Name = "s3ListBox",
                CheckOnClick = true
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
                Enabled = _currentUser.Role != UserRole.User ||
                         _currentUser.Role == UserRole.Executive
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
                headerLabel, bucketLabel, s3ListBox,
                listButton, downloadButton, deleteButton, refreshButton
            });

            return panel;
        }

        private void SetupUserInterface()
        {
            // Load S3 files on startup
            Task.Run(async () => await LoadS3Files());
        }

        private void BrowseButton_Click(object sender, EventArgs e)
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
                _localFiles = _fileService.GetLocalFiles(path);
                var localListBox = this.Controls.Find("localListBox", true).FirstOrDefault() as CheckedListBox;
                if (localListBox == null) return;

                localListBox.Items.Clear();

                foreach (var item in _localFiles)
                {
                    string displayText = item.IsDirectory ?
                        $"[DIR] {item.Name}" :
                        $"{item.Name} ({_fileService.FormatFileSize(item.Size)})";

                    localListBox.Items.Add(displayText);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading local files: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void UploadButton_Click(object sender, EventArgs e)
        {
            if (_currentUser.Role != UserRole.Administrator)
            {
                MessageBox.Show("Only administrators can upload files.", "Access Denied",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var localListBox = this.Controls.Find("localListBox", true).FirstOrDefault() as CheckedListBox;
            if (localListBox == null) return;

            var selectedItems = new List<LocalFileItem>();
            for (int i = 0; i < localListBox.CheckedIndices.Count; i++)
            {
                int index = localListBox.CheckedIndices[i];
                selectedItems.Add(_localFiles[index]);
            }

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

        private async void SyncButton_Click(object sender, EventArgs e)
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

                string folderName = System.IO.Path.GetFileName(_selectedLocalPath);
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

        private async void ListS3Button_Click(object sender, EventArgs e)
        {
            await LoadS3Files();
        }

        private async void RefreshS3Button_Click(object sender, EventArgs e)
        {
            await LoadS3Files();
        }

        private async Task LoadS3Files()
        {
            try
            {
                _s3Files = await _s3Service.ListFilesAsync(_currentUser.Role);
                var s3ListBox = this.Controls.Find("s3ListBox", true).FirstOrDefault() as CheckedListBox;
                if (s3ListBox == null) return;

                s3ListBox.Items.Clear();

                foreach (var item in _s3Files)
                {
                    string displayText = item.IsDirectory ?
                        $"[DIR] {item.Key}" :
                        $"{item.DisplayName} ({_fileService.FormatFileSize(item.Size)})";

                    s3ListBox.Items.Add(displayText);
                }

                var bucketLabel = this.Controls.Find("bucketLabel", true).FirstOrDefault() as Label;
                if (bucketLabel != null)
                {
                    var config = ConfigurationService.GetConfiguration();
                    bucketLabel.Text = $"Bucket: {config.AWS.BucketName} ({_s3Files.Count} items visible)";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading S3 files: {ex.Message}", "S3 Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void DownloadButton_Click(object sender, EventArgs e)
        {
            var s3ListBox = this.Controls.Find("s3ListBox", true).FirstOrDefault() as CheckedListBox;
            if (s3ListBox == null) return;

            var selectedItems = new List<S3FileItem>();
            for (int i = 0; i < s3ListBox.CheckedIndices.Count; i++)
            {
                int index = s3ListBox.CheckedIndices[i];
                selectedItems.Add(_s3Files[index]);
            }

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

                        foreach (var item in selectedItems.Where(i => !i.IsDirectory))
                        {
                            await _s3Service.DownloadFileAsync(item.Key, folderDialog.SelectedPath);
                        }

                        progressForm.Close();
                        MessageBox.Show("Download completed successfully!", "Success",
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

        private async void DeleteButton_Click(object sender, EventArgs e)
        {
            if (_currentUser.Role != UserRole.Administrator)
            {
                MessageBox.Show("Only administrators can delete files.", "Access Denied",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var s3ListBox = this.Controls.Find("s3ListBox", true).FirstOrDefault() as CheckedListBox;
            if (s3ListBox == null) return;

            var selectedItems = new List<S3FileItem>();
            for (int i = 0; i < s3ListBox.CheckedIndices.Count; i++)
            {
                int index = s3ListBox.CheckedIndices[i];
                selectedItems.Add(_s3Files[index]);
            }

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

                foreach (var item in selectedItems)
                {
                    await _s3Service.DeleteFileAsync(item.Key);
                }

                progressForm.Close();
                MessageBox.Show("Delete completed successfully!", "Success",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                await LoadS3Files();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting files: {ex.Message}", "Delete Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _s3Service?.Dispose();
            base.OnFormClosed(e);
        }
    }
}
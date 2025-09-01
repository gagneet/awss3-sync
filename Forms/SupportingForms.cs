// Forms/SupportingForms.cs - All Dialog Forms
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Amazon;
using Amazon.S3;
using S3FileManager.Models;
using S3FileManager.Services;

namespace S3FileManager
{
    // Sync Direction Selection
    public enum SyncDirection
    {
        LocalToS3,
        S3ToLocal
    }

    // Sync Summary and Confirmation Form
    public class SyncSummaryForm : Form
    {
        private readonly List<S3FileItem> _filesToDownload;
        private readonly List<S3FileItem> _filesToUpdate;
        private readonly List<LocalFileInfo> _localOnlyFiles;

        public bool UploadLocalOnlyFiles { get; private set; } = false;

        public SyncSummaryForm(List<S3FileItem> filesToDownload, List<S3FileItem> filesToUpdate, List<LocalFileInfo> localOnlyFiles)
        {
            _filesToDownload = filesToDownload;
            _filesToUpdate = filesToUpdate;
            _localOnlyFiles = localOnlyFiles;
            InitializeComponent();
            PopulateSummary();
        }

        private void InitializeComponent()
        {
            this.Size = new Size(700, 600);
            this.Text = "Sync Summary - Review Changes";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            var titleLabel = new Label
            {
                Text = "📋 Synchronization Summary",
                Font = new Font("Arial", 14, FontStyle.Bold),
                Location = new Point(20, 20),
                Size = new Size(650, 30),
                TextAlign = ContentAlignment.MiddleCenter
            };

            var tabControl = new TabControl
            {
                Location = new Point(20, 60),
                Size = new Size(650, 400),
                Name = "tabControl"
            };

            // Tab 1: Files to Download
            var downloadTab = new TabPage("📥 New Files from S3")
            {
                Name = "downloadTab"
            };

            var downloadLabel = new Label
            {
                Text = $"Files to download from S3 ({_filesToDownload.Count} files):",
                Font = new Font("Arial", 10, FontStyle.Bold),
                Location = new Point(10, 10),
                Size = new Size(600, 25)
            };

            var downloadListBox = new ListBox
            {
                Location = new Point(10, 40),
                Size = new Size(610, 320),
                Name = "downloadListBox"
            };

            downloadTab.Controls.AddRange(new Control[] { downloadLabel, downloadListBox });

            // Tab 2: Files to Update
            var updateTab = new TabPage("🔄 Files to Update")
            {
                Name = "updateTab"
            };

            var updateLabel = new Label
            {
                Text = $"Files to update (S3 version is newer) ({_filesToUpdate.Count} files):",
                Font = new Font("Arial", 10, FontStyle.Bold),
                Location = new Point(10, 10),
                Size = new Size(600, 25)
            };

            var updateListBox = new ListBox
            {
                Location = new Point(10, 40),
                Size = new Size(610, 320),
                Name = "updateListBox"
            };

            updateTab.Controls.AddRange(new Control[] { updateLabel, updateListBox });

            // Tab 3: Local Only Files
            var localTab = new TabPage("📁 Local Only Files")
            {
                Name = "localTab"
            };

            var localLabel = new Label
            {
                Text = $"Files that exist only locally ({_localOnlyFiles.Count} files):",
                Font = new Font("Arial", 10, FontStyle.Bold),
                Location = new Point(10, 10),
                Size = new Size(600, 25)
            };

            var localListBox = new ListBox
            {
                Location = new Point(10, 40),
                Size = new Size(610, 280),
                Name = "localListBox"
            };

            var uploadLocalCheckBox = new CheckBox
            {
                Text = "📤 Upload these local files to S3 (you'll choose permissions next)",
                Location = new Point(10, 330),
                Size = new Size(600, 25),
                Name = "uploadLocalCheckBox",
                Font = new Font("Arial", 9, FontStyle.Bold),
                ForeColor = Color.DarkBlue
            };

            localTab.Controls.AddRange(new Control[] { localLabel, localListBox, uploadLocalCheckBox });

            tabControl.TabPages.AddRange(new TabPage[] { downloadTab, updateTab, localTab });

            // Summary section
            var summaryLabel = new Label
            {
                Text = "Summary:",
                Font = new Font("Arial", 11, FontStyle.Bold),
                Location = new Point(20, 480),
                Size = new Size(100, 25)
            };

            var summaryText = new Label
            {
                Text = $"• {_filesToDownload.Count} new files will be downloaded\n" +
                       $"• {_filesToUpdate.Count} files will be updated\n" +
                       $"• {_localOnlyFiles.Count} local-only files found",
                Location = new Point(40, 510),
                Size = new Size(400, 60),
                ForeColor = Color.DarkGreen
            };

            // Buttons
            var proceedButton = new Button
            {
                Text = "✅ Proceed with Sync",
                Location = new Point(450, 520),
                Size = new Size(130, 35),
                DialogResult = DialogResult.OK,
                Font = new Font("Arial", 10, FontStyle.Bold),
                BackColor = Color.LightGreen
            };
            proceedButton.Click += ProceedButton_Click;

            var cancelButton = new Button
            {
                Text = "❌ Cancel",
                Location = new Point(590, 520),
                Size = new Size(80, 35),
                DialogResult = DialogResult.Cancel
            };

            this.Controls.AddRange(new Control[]
            {
                titleLabel, tabControl, summaryLabel, summaryText, proceedButton, cancelButton
            });

            this.AcceptButton = proceedButton;
            this.CancelButton = cancelButton;
        }

        private void PopulateSummary()
        {
            // Populate download list
            var downloadListBox = this.Controls.Find("downloadListBox", true)[0] as ListBox;
            if (downloadListBox != null)
            {
                foreach (var file in _filesToDownload)
                {
                    downloadListBox.Items.Add($"📄 {file.Key} ({FormatFileSize(file.Size)})");
                }

                if (_filesToDownload.Count == 0)
                {
                    downloadListBox.Items.Add("✅ No new files to download - everything is up to date!");
                }
            }

            // Populate update list
            var updateListBox = this.Controls.Find("updateListBox", true)[0] as ListBox;
            if (updateListBox != null)
            {
                foreach (var file in _filesToUpdate)
                {
                    updateListBox.Items.Add($"🔄 {file.Key} ({FormatFileSize(file.Size)}) - newer version available");
                }

                if (_filesToUpdate.Count == 0)
                {
                    updateListBox.Items.Add("✅ No files need updating - everything is current!");
                }
            }

            // Populate local-only list
            var localListBox = this.Controls.Find("localListBox", true)[0] as ListBox;
            if (localListBox != null)
            {
                foreach (var file in _localOnlyFiles)
                {
                    localListBox.Items.Add($"📁 {file.RelativePath} ({FormatFileSize(file.Size)}) - local only");
                }

                if (_localOnlyFiles.Count == 0)
                {
                    localListBox.Items.Add("✅ No local-only files found - perfect sync!");
                }
            }

            // Enable/disable upload checkbox based on local files
            var uploadCheckBox = this.Controls.Find("uploadLocalCheckBox", true)[0] as CheckBox;
            if (uploadCheckBox != null)
            {
                uploadCheckBox.Enabled = _localOnlyFiles.Count > 0;
                if (_localOnlyFiles.Count == 0)
                {
                    uploadCheckBox.Text = "No local-only files to upload";
                    uploadCheckBox.ForeColor = Color.Gray;
                }
            }
        }

        private void ProceedButton_Click(object? sender, EventArgs e)
        {
            var uploadCheckBox = this.Controls.Find("uploadLocalCheckBox", true)[0] as CheckBox;
            if (uploadCheckBox != null)
            {
                UploadLocalOnlyFiles = uploadCheckBox.Checked;
            }
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }

    // Sync Direction Selection Form
    public class SyncDirectionForm : Form
    {
        public SyncDirection SyncDirection { get; private set; } = SyncDirection.LocalToS3;

        public SyncDirectionForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Size = new Size(450, 350);
            this.Text = "Choose Sync Direction";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            var titleLabel = new Label
            {
                Text = "Sync Direction",
                Font = new Font("Arial", 14, FontStyle.Bold),
                Location = new Point(20, 20),
                Size = new Size(400, 30),
                TextAlign = ContentAlignment.MiddleCenter
            };

            var instructionLabel = new Label
            {
                Text = "Choose the direction for synchronization:",
                Font = new Font("Arial", 10),
                Location = new Point(20, 60),
                Size = new Size(400, 25)
            };

            // Local to S3 option
            var localToS3RadioButton = new RadioButton
            {
                Text = "📤 Upload Local Files to S3",
                Font = new Font("Arial", 11, FontStyle.Bold),
                Location = new Point(30, 100),
                Size = new Size(380, 25),
                Checked = true,
                Name = "localToS3Radio"
            };

            var localToS3Description = new Label
            {
                Text = "• Upload files from your selected local folder to the S3 bucket\n" +
                       "• Creates new files in S3 or overwrites existing ones\n" +
                       "• You can choose access permissions for uploaded files",
                Location = new Point(50, 130),
                Size = new Size(350, 60),
                ForeColor = Color.DarkBlue
            };

            // S3 to Local option
            var s3ToLocalRadioButton = new RadioButton
            {
                Text = "📥 Download S3 Files to Local",
                Font = new Font("Arial", 11, FontStyle.Bold),
                Location = new Point(30, 200),
                Size = new Size(380, 25),
                Name = "s3ToLocalRadio"
            };

            var s3ToLocalDescription = new Label
            {
                Text = "• Download files from S3 bucket to your selected local folder\n" +
                       "• Only downloads files you have permission to access\n" +
                       "• Maintains S3 folder structure in local directory",
                Location = new Point(50, 230),
                Size = new Size(350, 60),
                ForeColor = Color.DarkGreen
            };

            var syncButton = new Button
            {
                Text = "Start Sync",
                Location = new Point(270, 300),
                Size = new Size(90, 35),
                DialogResult = DialogResult.OK,
                Font = new Font("Arial", 10, FontStyle.Bold),
                BackColor = Color.LightBlue
            };
            syncButton.Click += SyncButton_Click;

            var cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(370, 300),
                Size = new Size(70, 35),
                DialogResult = DialogResult.Cancel
            };

            this.Controls.AddRange(new Control[]
            {
                titleLabel, instructionLabel,
                localToS3RadioButton, localToS3Description,
                s3ToLocalRadioButton, s3ToLocalDescription,
                syncButton, cancelButton
            });

            this.AcceptButton = syncButton;
            this.CancelButton = cancelButton;
        }

        private void SyncButton_Click(object? sender, EventArgs e)
        {
            var localToS3Radio = this.Controls.Find("localToS3Radio", false)[0] as RadioButton;
            var s3ToLocalRadio = this.Controls.Find("s3ToLocalRadio", false)[0] as RadioButton;

            if (localToS3Radio?.Checked == true)
            {
                SyncDirection = SyncDirection.LocalToS3;
            }
            else if (s3ToLocalRadio?.Checked == true)
            {
                SyncDirection = SyncDirection.S3ToLocal;
            }
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
                
                // Create S3 client and MetadataService similar to S3Service
                var config = ConfigurationService.GetConfiguration();
                var awsConfig = new Amazon.S3.AmazonS3Config { RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(config.AWS.Region) };
                using var s3Client = new Amazon.S3.AmazonS3Client(config.AWS.AccessKey, config.AWS.SecretKey, awsConfig);
                var metadataService = new MetadataService(s3Client, config.AWS.BucketName);

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
}
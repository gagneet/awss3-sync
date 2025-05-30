﻿// Forms/MainForm.cs - Core Implementation
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
        // Core fields - shared across all partial files
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

        public MainForm(User currentUser)
        {
            _currentUser = currentUser;
            _s3Service = new S3Service();
            _fileService = new FileService();
            InitializeComponent();
            this.Text = $"AWS S3 File Manager - {_currentUser.Username} ({_currentUser.Role})";
            SetupUserInterface();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.AutoScaleDimensions = new SizeF(8F, 16F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(1400, 900);
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
                Scrollable = true
            };

            // Event handlers
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
                Scrollable = true
            };

            // Event handlers
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

            // Search controls
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

            // Load S3 files on startup
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

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _s3Service?.Dispose();
            base.OnFormClosed(e);
        }
    }
}
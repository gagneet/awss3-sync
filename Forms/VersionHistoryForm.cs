using System;
using System.Collections.Generic;
using System.Windows.Forms;
using AWSS3Sync.Models;
using AWSS3Sync.Services;

namespace AWSS3Sync.Forms
{
    public class VersionHistoryForm : Form
    {
        private readonly S3Service _s3Service;
        private readonly FileService _fileService;
        private readonly string _s3Key;
        private readonly List<FileNode> _versions;

        private ListView listViewVersions;
        private Button btnDownload;

        public VersionHistoryForm(string s3Key, List<FileNode> versions, S3Service s3Service, FileService fileService)
        {
            _s3Key = s3Key;
            _versions = versions;
            _s3Service = s3Service;
            _fileService = fileService;

            InitializeComponent();
            PopulateVersions();
        }

        private void InitializeComponent()
        {
            this.Text = $"Version History for {_s3Key}";
            this.ClientSize = new System.Drawing.Size(600, 400);

            listViewVersions = new ListView
            {
                Location = new System.Drawing.Point(10, 10),
                Size = new System.Drawing.Size(580, 340),
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false
            };
            listViewVersions.Columns.Add("Version ID", 250);
            listViewVersions.Columns.Add("Last Modified", 150);
            listViewVersions.Columns.Add("Size", 80);

            btnDownload = new Button
            {
                Text = "Download Selected Version",
                Location = new System.Drawing.Point(400, 360),
                Size = new System.Drawing.Size(190, 30)
            };
            btnDownload.Click += BtnDownload_Click;

            this.Controls.Add(listViewVersions);
            this.Controls.Add(btnDownload);
        }

        private void PopulateVersions()
        {
            foreach (var version in _versions)
            {
                var item = new ListViewItem(version.VersionId);
                item.SubItems.Add(version.LastModified.ToString("g"));
                item.SubItems.Add(_fileService.FormatFileSize(version.Size));
                item.Tag = version;
                listViewVersions.Items.Add(item);
            }
        }

        private async void BtnDownload_Click(object sender, EventArgs e)
        {
            if (listViewVersions.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select a version to download.", "No Version Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selectedVersion = listViewVersions.SelectedItems[0].Tag as FileNode;
            if (selectedVersion == null) return;

            using (var folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Select a folder to download the file to";
                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        await _s3Service.DownloadFileAsync(_s3Key, folderDialog.SelectedPath, selectedVersion.VersionId);
                        MessageBox.Show($"Successfully downloaded version '{selectedVersion.VersionId}' to '{folderDialog.SelectedPath}'.", "Download Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Download failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
    }
}

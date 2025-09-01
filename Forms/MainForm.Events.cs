using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using S3FileManager.Models;

namespace S3FileManager
{
    public partial class MainForm
    {
        private async void TreeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            var fileNode = e.Node?.Tag as FileNode;
            if (fileNode == null) return;

            var previewInfoLabel = this.Controls.Find("previewInfoLabel", true).FirstOrDefault() as Label;
            var previewTextBox = this.Controls.Find("previewTextBox", true).FirstOrDefault() as RichTextBox;
            var previewPictureBox = this.Controls.Find("previewPictureBox", true).FirstOrDefault() as PictureBox;

            // Reset preview controls
            previewInfoLabel.Visible = true;
            previewTextBox.Visible = false;
            previewPictureBox.Visible = false;
            previewTextBox.Clear();
            if (previewPictureBox.Image != null)
            {
                previewPictureBox.Image.Dispose();
                previewPictureBox.Image = null;
            }

            if (fileNode.IsDirectory)
            {
                previewInfoLabel.Text = $"Directory: {fileNode.Name}";
                return;
            }

            previewInfoLabel.Text = $"File: {fileNode.Name}\nSize: {_fileService.FormatFileSize(fileNode.Size)}\nLast Modified: {fileNode.LastModified}";

            string tempFilePath = null;
            try
            {
                string filePathToRead = fileNode.Path;
                if (fileNode.IsS3)
                {
                    string tempPreviewDir = Path.Combine(Path.GetTempPath(), "AWSS3Sync_Previews");
                    Directory.CreateDirectory(tempPreviewDir);
                    tempFilePath = await _s3Service.DownloadFileAsync(fileNode.Path, tempPreviewDir);
                    filePathToRead = tempFilePath;
                }

                var extension = Path.GetExtension(fileNode.Name).ToLowerInvariant();
                if (new[] { ".txt", ".log", ".json", ".xml", ".cs", ".js", ".html", ".css" }.Contains(extension))
                {
                    previewTextBox.Text = File.ReadAllText(filePathToRead);
                    previewTextBox.Visible = true;
                    previewInfoLabel.Visible = false;
                }
                else if (new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" }.Contains(extension))
                {
                    using (var stream = new MemoryStream(File.ReadAllBytes(filePathToRead)))
                    {
                        previewPictureBox.Image = Image.FromStream(stream);
                    }
                    previewPictureBox.Visible = true;
                    previewInfoLabel.Visible = false;
                }
                else
                {
                    previewInfoLabel.Text += "\n\nPreview for this file type is not supported.";
                }
            }
            catch (Exception ex)
            {
                previewInfoLabel.Text += $"\n\nError loading preview: {ex.Message}";
            }
            finally
            {
                if (tempFilePath != null && File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
        }
        private void LocalTreeView_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            if (e.Node.Nodes.Count > 0 && e.Node.Nodes[0].Text == "Loading...")
            {
                e.Node.Nodes.Clear();
                if (e.Node.Tag is FileNode fileNode)
                {
                    LoadLocalDirectoryNodes(e.Node, fileNode.Path);
                }
            }
        }


        private void TreeView_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (e.Node?.Tag is FileNode fileNode)
            {
                var isLocal = sender == this.Controls.Find("localTreeView", true).FirstOrDefault();
                var checkedItems = isLocal ? _localCheckedItems : _s3CheckedItems;
                checkedItems[fileNode.Path] = e.Node.Checked;

                if (fileNode.IsDirectory)
                {
                    CheckAllChildNodes(e.Node, e.Node.Checked);
                }

                if (isLocal)
                {
                    UpdateLocalSelectionCount();
                }
                else
                {
                    UpdateS3SelectionCount();
                }
            }
        }

        private void CheckAllChildNodes(TreeNode treeNode, bool nodeChecked)
        {
            foreach (TreeNode node in treeNode.Nodes)
            {
                node.Checked = nodeChecked;
                if (node.Tag is FileNode fileNode)
                {
                     var isLocal = treeNode.TreeView.Name == "localTreeView";
                     var checkedItems = isLocal ? _localCheckedItems : _s3CheckedItems;
                     checkedItems[fileNode.Path] = nodeChecked;
                }
                if (node.Nodes.Count > 0)
                {
                    CheckAllChildNodes(node, nodeChecked);
                }
            }
        }

        #region Button Event Handlers

        private void BrowseButton_Click(object sender, EventArgs e)
        {
            using (var folderDialog = new FolderBrowserDialog())
            {
                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    _selectedLocalPath = folderDialog.SelectedPath;
                    var pathLabel = this.Controls.Find("pathLabel", true).FirstOrDefault() as Label;
                    if (pathLabel != null)
                    {
                        pathLabel.Text = $"Selected Path: {_selectedLocalPath}";
                    }
                    LoadLocalFiles(_selectedLocalPath);
                }
            }
        }

        private async void UploadButton_Click(object sender, EventArgs e)
        {
            var selectedItems = GetCheckedLocalItems();
            if (selectedItems.Count == 0)
            {
                MessageBox.Show("Please select files or folders to upload.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Simplified for now, will need to be updated with role logic
            var roles = new List<UserRole> { _currentUser.Role };

            foreach (var item in selectedItems)
            {
                if (item.IsDirectory)
                {
                    await _s3Service.UploadDirectoryAsync(item.Path, item.Name, roles);
                }
                else
                {
                    await _s3Service.UploadFileAsync(item.Path, item.Name, roles);
                }
            }
            MessageBox.Show("Upload complete.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await LoadS3FilesAsync();
        }

        private async void DownloadButton_Click(object sender, EventArgs e)
        {
            var selectedItems = GetCheckedS3Items();
            if (selectedItems.Count == 0)
            {
                MessageBox.Show("Please select files or folders to download.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "S3Downloads");
            Directory.CreateDirectory(downloadPath);

            foreach (var item in selectedItems)
            {
                await _s3Service.DownloadFileAsync(item.Path, downloadPath);
            }
            MessageBox.Show($"Downloaded to {downloadPath}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async void DeleteButton_Click(object sender, EventArgs e)
        {
            var selectedItems = GetCheckedS3Items();
            if (selectedItems.Count == 0)
            {
                MessageBox.Show("Please select files or folders to delete.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show("Are you sure you want to delete the selected items?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                try
                {
                    foreach (var item in selectedItems)
                    {
                        await _s3Service.DeleteFileAsync(item.Path, _currentUser.Role);
                    }
                    MessageBox.Show("Deletion complete.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    await LoadS3FilesAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Deletion failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async void SyncButton_Click(object sender, EventArgs e)
        {
            if (_currentUser.Role != UserRole.Administrator)
            {
                MessageBox.Show("Only administrators can sync folders.", "Access Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrEmpty(_selectedLocalPath) || !Directory.Exists(_selectedLocalPath))
            {
                MessageBox.Show("Please select a valid local folder for synchronization.", "No Local Folder Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var checkedS3Items = GetCheckedS3Items();
            var s3Folder = checkedS3Items.FirstOrDefault(i => i.IsDirectory);

            if (s3Folder == null)
            {
                MessageBox.Show("Please select a single S3 folder to sync with.", "No S3 Folder Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // The SyncDirectionForm is not available in the provided code, so this will cause a compile error.
            // This is a placeholder to show the intended logic.
            // var syncForm = new SyncDirectionForm();
            // if (syncForm.ShowDialog() != DialogResult.OK)
            //     return;

            MessageBox.Show("Sync functionality is not fully implemented due to missing UI components (SyncDirectionForm).", "Not Implemented", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async void ListS3Button_Click(object sender, EventArgs e)
        {
            await LoadS3FilesAsync();
        }

        private async void RefreshS3Button_Click(object sender, EventArgs e)
        {
            await LoadS3FilesAsync();
        }

        #endregion
    }
}

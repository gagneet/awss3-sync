using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using AWSS3Sync.Models;
using AWSS3Sync.Services;

namespace AWSS3Sync
{
    public partial class MainForm
    {
        private async void TreeView_AfterSelect(object? sender, TreeViewEventArgs e)
        {
            var fileNode = e.Node?.Tag as FileNode;
            if (fileNode == null) return;

            // Use the class fields directly, which are initialized in CreatePreviewPanel()
            // This avoids repeated, inefficient calls to Controls.Find() and makes the code cleaner.

            // Reset preview controls
            previewInfoLabel.Visible = true;
            previewTextBox.Visible = false;
            previewTextBox.Clear();

            previewPictureBox.Visible = false;
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

            string? tempFilePath = null;
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
        private void LocalTreeView_BeforeExpand(object? sender, TreeViewCancelEventArgs e)
        {
            if (e.Node != null && e.Node.Nodes.Count > 0 && e.Node.Nodes[0].Text == "Loading...")
            {
                e.Node.Nodes.Clear();
                if (e.Node.Tag is FileNode fileNode)
                {
                    LoadLocalDirectoryNodes(e.Node, fileNode.Path);
                }
            }
        }

        private void S3TreeView_MouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                if (sender is TreeView treeView)
                {
                    var node = treeView.GetNodeAt(e.X, e.Y);
                    if (node != null)
                    {
                        treeView.SelectedNode = node;
                    }
                }
            }
        }

        private void S3ContextMenu_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (sender is not ContextMenuStrip menu) return;

            var treeView = menu.SourceControl as TreeView;
            var selectedNode = treeView?.SelectedNode;

            var viewVersionsMenuItem = menu.Items.Find("View Versions", false).FirstOrDefault();
            if (viewVersionsMenuItem == null) return;

            if (selectedNode?.Tag is FileNode fileNode && !fileNode.IsDirectory)
            {
                viewVersionsMenuItem.Enabled = true;
            }
            else
            {
                viewVersionsMenuItem.Enabled = false;
            }
        }

        private async void ViewVersionsMenuItem_Click(object? sender, EventArgs e)
        {
            var s3TreeView = this.Controls.Find("s3TreeView", true).FirstOrDefault() as TreeView;
            var selectedNode = s3TreeView?.SelectedNode;

            if (!(selectedNode?.Tag is FileNode fileNode) || fileNode.IsDirectory)
            {
                return; // Should be disabled by the Opening event, but check again.
            }

            try
            {
                var versions = await _s3Service.GetFileVersionsAsync(fileNode.Path);
                if (versions == null || !versions.Any())
                {
                    MessageBox.Show("No version history found for this file.", "No Versions", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // The namespace of this form is AWSS3Sync, and VersionHistoryForm is in AWSS3Sync.Forms.
                // We can access it via Forms.VersionHistoryForm.
                using (var versionForm = new Forms.VersionHistoryForm(fileNode.Path, versions, _s3Service, _fileService))
                {
                    versionForm.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error retrieving version history: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void S3TreeView_BeforeExpand(object? sender, TreeViewCancelEventArgs e)
        {
            if (e.Node != null && e.Node.Nodes.Count > 0 && e.Node.Nodes[0].Text == "Loading...")
            {
                await LoadS3DirectoryNodesAsync(e.Node);
            }
        }

        private void TreeView_AfterCheck(object? sender, TreeViewEventArgs e)
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
                if (node.Tag is FileNode fileNode && treeNode.TreeView != null)
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

        private void BrowseButton_Click(object? sender, EventArgs e)
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

        private async void UploadButton_Click(object? sender, EventArgs e)
        {
            // If a comparison has been run, perform a delta upload
            if (_comparisonResults.Any())
            {
                var toUpload = _comparisonResults.Where(r => r.Status == ComparisonStatus.LocalOnly || r.Status == ComparisonStatus.Modified).ToList();
                if (!toUpload.Any())
                {
                    MessageBox.Show("No new or modified local files to upload.", "Nothing to Upload", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var confirmation = MessageBox.Show($"This will upload {toUpload.Count(r => r.Status == ComparisonStatus.LocalOnly)} new file(s) and {toUpload.Count(r => r.Status == ComparisonStatus.Modified)} modified file(s) to S3. Continue?", "Confirm Delta Upload", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirmation == DialogResult.No) return;

                var s3TreeView = this.Controls.Find("s3TreeView", true).FirstOrDefault() as TreeView;
                var selectedS3Node = s3TreeView?.SelectedNode;
                var s3Dir = selectedS3Node?.Tag as FileNode;

                if (s3Dir == null)
                {
                    MessageBox.Show("Could not determine the S3 destination directory. Please select an S3 folder.", "Destination Missing", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                foreach (var result in toUpload)
                {
                    if (result.LocalFile != null)
                    {
                        var s3Key = Path.Combine(s3Dir.Path, result.RelativePath).Replace('\\', '/');
                        var roles = new List<UserRole> { _currentUser.Role };
                        await _s3Service.UploadFileAsync(result.LocalFile.Path, s3Key, roles);
                    }
                }
                MessageBox.Show("Delta upload complete.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else // Otherwise, perform a standard selection-based upload
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
            }

            // Refresh the S3 view and re-run comparison to show updated state
            await LoadS3FilesAsync();
            if (_comparisonResults.Any())
            {
                CompareButton_Click(this, EventArgs.Empty);
            }
        }

        private async void DownloadButton_Click(object? sender, EventArgs e)
        {
            // If a comparison has been run, perform a delta download
            if (_comparisonResults.Any())
            {
                var toDownload = _comparisonResults.Where(r => r.Status == ComparisonStatus.S3Only || r.Status == ComparisonStatus.Modified).ToList();
                if (!toDownload.Any())
                {
                    MessageBox.Show("No new or modified S3 files to download.", "Nothing to Download", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var confirmation = MessageBox.Show($"This will download {toDownload.Count(r => r.Status == ComparisonStatus.S3Only)} new file(s) and {toDownload.Count(r => r.Status == ComparisonStatus.Modified)} modified file(s) from S3. Continue?", "Confirm Delta Download", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirmation == DialogResult.No) return;

                var localTreeView = this.Controls.Find("localTreeView", true).FirstOrDefault() as TreeView;
                var selectedLocalNode = localTreeView?.SelectedNode;
                var localDir = selectedLocalNode?.Tag as FileNode;

                if (localDir == null)
                {
                    MessageBox.Show("Could not determine the local destination directory. Please select a local folder.", "Destination Missing", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                foreach (var result in toDownload)
                {
                    if (result.S3File != null)
                    {
                        var localPath = Path.Combine(localDir.Path, result.RelativePath);
                        var localDirForFile = Path.GetDirectoryName(localPath);
                        if (localDirForFile != null)
                        {
                            if (!Directory.Exists(localDirForFile))
                            {
                                Directory.CreateDirectory(localDirForFile);
                            }
                            await _s3Service.DownloadFileAsync(result.S3File.Path, localDirForFile);
                        }
                    }
                }
                MessageBox.Show("Delta download complete.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else // Otherwise, perform a standard selection-based download
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
                    // This is a simplified download, a real implementation would handle folders.
                    if (!item.IsDirectory)
                    {
                        await _s3Service.DownloadFileAsync(item.Path, downloadPath);
                    }
                }
                MessageBox.Show($"Downloaded to {downloadPath}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            // Refresh the local view and re-run comparison to show updated state
            if (!string.IsNullOrEmpty(_selectedLocalPath))
            {
                LoadLocalFiles(_selectedLocalPath);
            }
            if (_comparisonResults.Any())
            {
                CompareButton_Click(this, EventArgs.Empty);
            }
        }

        private async void DeleteButton_Click(object? sender, EventArgs e)
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

        private void SyncButton_Click(object? sender, EventArgs e)
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

        private async void ListS3Button_Click(object? sender, EventArgs e)
        {
            await LoadS3FilesAsync();
        }

        private async void RefreshS3Button_Click(object? sender, EventArgs e)
        {
            // Reload the entire S3 tree from the root
            await LoadS3FilesAsync();
        }

        private void ManagePermissionsButton_Click(object? sender, EventArgs e)
        {
            // Placeholder stub for permissions management functionality
            MessageBox.Show("Permissions management functionality not yet implemented.", "Not Implemented", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void SearchTextBox_TextChanged(object? sender, EventArgs e)
        {
            // Placeholder stub for search functionality
            // This would typically filter the S3 TreeView based on the search text
        }

        private void ClearSearchButton_Click(object? sender, EventArgs e)
        {
            // Placeholder stub for clearing search
            var searchTextBox = this.Controls.Find("searchTextBox", true).FirstOrDefault() as TextBox;
            if (searchTextBox != null)
            {
                searchTextBox.Clear();
            }
        }

        private async void CompareButton_Click(object? sender, EventArgs e)
        {
            var localTreeView = this.Controls.Find("localTreeView", true).FirstOrDefault() as TreeView;
            var s3TreeView = this.Controls.Find("s3TreeView", true).FirstOrDefault() as TreeView;

            var selectedLocalNode = localTreeView?.SelectedNode;
            var selectedS3Node = s3TreeView?.SelectedNode;

            if (!(selectedLocalNode?.Tag is FileNode localDir) || !localDir.IsDirectory ||
                !(selectedS3Node?.Tag is FileNode s3Dir) || !s3Dir.IsDirectory)
            {
                MessageBox.Show("Please select a local folder and an S3 folder to compare.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Reset colors before running a new comparison
            ResetTreeViewColors();

            try
            {
                var localFiles = _fileService.GetAllFiles(localDir.Path);
                var s3Files = await _s3Service.ListAllFilesAsync(s3Dir.Path, _currentUser.Role);

                _comparisonResults = _comparisonService.CompareDirectories(localDir, s3Dir, localFiles, s3Files);

                ApplyComparisonToTrees();

                MessageBox.Show($"Comparison complete. Found {_comparisonResults.Count(r => r.Status != ComparisonStatus.Identical)} differences.", "Comparison Finished", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred during comparison: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ReviewPermissionsButton_Click(object? sender, EventArgs e)
        {
            if (_currentUser.Role != UserRole.Administrator)
            {
                MessageBox.Show("Only administrators can review pending permissions.", "Access Denied", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var autoTaggedFiles = MetadataService.GetAutoTaggedFiles();
            if (autoTaggedFiles.Count == 0)
            {
                MessageBox.Show("No files require permission review. All files have proper permission tags.", 
                    "No Pending Permissions", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var reviewForm = new AdminPermissionReviewForm();
            reviewForm.ShowDialog();
        }

        #endregion
    }
}

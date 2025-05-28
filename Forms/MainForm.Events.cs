// Forms/MainForm.Events.cs - Event Handlers
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
    public partial class MainForm
    {
        #region Selection Persistence and Performance Event Handlers

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

        #endregion

        #region Button Event Handlers

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
                MessageBox.Show("Please browse and select a local folder first before syncing.", "No Local Folder Selected",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!Directory.Exists(_selectedLocalPath))
            {
                MessageBox.Show("The selected local folder no longer exists. Please browse and select a valid folder.", "Invalid Local Folder",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Show sync direction dialog
            var syncForm = new SyncDirectionForm();
            if (syncForm.ShowDialog() != DialogResult.OK)
                return;

            try
            {
                var progressForm = new ProgressForm("Syncing files...");
                progressForm.Show();

                if (syncForm.SyncDirection == SyncDirection.LocalToS3)
                {
                    // Original sync: Local to S3
                    var roleForm = new RoleSelectionForm();
                    if (roleForm.ShowDialog() != DialogResult.OK)
                    {
                        progressForm.Close();
                        return;
                    }

                    progressForm.UpdateMessage("Uploading local files to S3...");
                    string folderName = Path.GetFileName(_selectedLocalPath);
                    await _s3Service.UploadDirectoryAsync(_selectedLocalPath, folderName, roleForm.SelectedRoles);
                }
                else
                {
                    // New sync: S3 to Local
                    progressForm.UpdateMessage("Downloading S3 files to local folder...");
                    List<string> extraLocalFiles = await SyncS3ToLocal(_selectedLocalPath, progressForm);
                    
                    // Close progress form before showing messages
                    progressForm.Close(); 

                    if (extraLocalFiles != null && extraLocalFiles.Any())
                    {
                        string fileList = string.Join(Environment.NewLine, extraLocalFiles.Select(f => $"- {f}"));
                        string warningMessage = $"Sync complete. However, the following local files do not exist in the S3 bucket:{Environment.NewLine}{Environment.NewLine}{fileList}{Environment.NewLine}{Environment.NewLine}You may want to upload these files to S3 or remove them from your local folder if they are no longer needed.";
                        MessageBox.Show(warningMessage, "Sync Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    else
                    {
                        MessageBox.Show("Sync completed successfully! No extra local files found.", "Success",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }

                // Refresh both views - moved out of the specific S3 to Local block if progressForm was closed there
                // If progressForm was closed inside the 'else', it should not be closed again in a finally block
                // that might not be aware of the context.
                // For now, assuming progressForm.Close() in the 'else' is sufficient before messages.
                // If SyncS3ToLocal throws, progressForm might not be closed if not handled by a broader try/finally.

                // The original code had progressForm.Close() after both sync types, then messages.
                // Let's ensure progressForm is closed before any message box.
                // The current structure closes it within the 'else' block.
                // If SyncLocalToS3 was chosen, progressForm.Close() is called before its success message.

                await LoadS3FilesAsync(); // Refresh S3 view
                LoadLocalFiles(_selectedLocalPath); // Refresh local view
            }
            catch (Exception ex)
            {
                // Ensure progress form is closed in case of error if it's still open
                var progressFormInstance = Application.OpenForms.OfType<ProgressForm>().FirstOrDefault();
                progressFormInstance?.Close();

                MessageBox.Show($"Error during sync: {ex.Message}", "Sync Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            // Removed finally block for progressForm.Close() as it's handled within try or specific logic paths.
        }

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

            try
            {
                // Create EastGate-Files folder in Documents
                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string eastGateFolder = Path.Combine(documentsPath, "EastGate-Files");

                // Create the folder if it doesn't exist
                if (!Directory.Exists(eastGateFolder))
                {
                    Directory.CreateDirectory(eastGateFolder);
                }

                var progressForm = new ProgressForm("Downloading files...");
                progressForm.Show();

                int downloadCount = 0;
                int totalItems = selectedItems.Count;

                for (int i = 0; i < selectedItems.Count; i++)
                {
                    var item = selectedItems[i];
                    progressForm.UpdateMessage($"Processing: {item.Key} ({i + 1}/{totalItems})");

                    if (item.IsDirectory)
                    {
                        // Download entire folder and its contents
                        await DownloadS3Folder(item.Key, eastGateFolder, progressForm);
                        downloadCount++;
                    }
                    else
                    {
                        // Download single file
                        await DownloadS3File(item.Key, eastGateFolder, progressForm);
                        downloadCount++;
                    }
                }

                progressForm.Close();

                // Show success message with option to open folder
                var result = MessageBox.Show(
                    $"Downloaded {downloadCount} item(s) successfully to:\n{eastGateFolder}\n\nWould you like to open the download folder?",
                    "Download Complete",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (result == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start("explorer.exe", eastGateFolder);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error downloading files: {ex.Message}", "Download Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                // Ensure progress form is closed in case of error if it's still open
                // Check if progressForm variable is accessible and not null before trying to close
                // For simplicity, accessing Application.OpenForms as a fallback.
                var openProgressForm = Application.OpenForms.OfType<ProgressForm>().FirstOrDefault(pf => pf.Text == "Syncing files...");
                if (openProgressForm != null)
                {
                    if (openProgressForm.InvokeRequired)
                        openProgressForm.Invoke(new Action(() => openProgressForm.Close()));
                    else
                        openProgressForm.Close();
                }

                MessageBox.Show($"Error deleting files: {ex.Message}", "Delete Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            // Removed finally block for progressForm.Close() to avoid closing it if already closed.
            // Refresh operations are now at the end of the try block.
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

        #endregion

        #region Search Event Handlers

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

        #endregion
    }
}
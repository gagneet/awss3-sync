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
            var node = e.Node;
            if (node != null && node.Nodes.Count == 1 && node.Nodes[0].Text == "Loading...")
            {
                if (node.Tag is S3FileItem parentItem && parentItem.IsDirectory)
                {
                    var treeView = sender as TreeView;
                    if (treeView == null) return;

                    _isUpdatingTree = true; 
                    treeView.BeginUpdate();
                    node.Nodes.Clear(); // Remove "Loading..."

                    string parentPrefix = parentItem.Key; 
                    if (!parentPrefix.EndsWith("/")) parentPrefix += "/";

                    var directChildrenKeys = new HashSet<string>();
                    if (_s3Files != null)
                    {
                        foreach (var s3File in _s3Files)
                        {
                            if (s3File.Key.StartsWith(parentPrefix) && s3File.Key.Length > parentPrefix.Length)
                            {
                                string remainingPath = s3File.Key.Substring(parentPrefix.Length);
                                var pathParts = remainingPath.Split(new[] {'/'}, StringSplitOptions.RemoveEmptyEntries);
                                if (pathParts.Length > 0)
                                {
                                    string childName = pathParts[0];
                                    // Determine if this child is a folder or a file based on the original key or if other files imply it's a folder
                                    bool isActualDirectory = s3File.Key.EndsWith("/") || (pathParts.Length > 1);
                                    // Or, if an explicit S3 object exists that is this prefix and is a directory
                                    var explicitChildFolder = _s3Files.FirstOrDefault(f => f.Key == parentPrefix + childName + "/" && f.IsDirectory);
                                    if (explicitChildFolder != null) isActualDirectory = true;
                                    
                                    string childFullKey = parentPrefix + childName + (isActualDirectory ? "/" : "");
                                    directChildrenKeys.Add(childFullKey);
                                }
                            }
                        }
                    }
            
                    foreach (string childKey in directChildrenKeys.OrderBy(k => k))
                    {
                        S3FileItem childItem = _s3Files.FirstOrDefault(f => f.Key == childKey);
                        // bool isImplicitFolder = false; // Not strictly needed if Key defines IsDirectory
                        if (childItem == null) // Implicit item (folder or file)
                        {
                             // Key for childItem is childKey. S3FileItem's IsDirectory will be set based on childKey ending with "/"
                             childItem = new S3FileItem { Key = childKey, Size = 0, LastModified = DateTime.MinValue };
                             // isImplicitFolder = childKey.EndsWith("/"); // Can be used if needed for other logic
                        }
                        // No direct assignment to childItem.IsDirectory needed.
                        // The childFullKey logic (parentPrefix + childName + (isActualDirectory ? "/" : ""))
                        // already ensures childKey (which is childFullKey) ends with "/" if it's a directory.

                        // Replicating AddSingleS3NodeToCollection's core logic here
                        string displayName = childItem.Key.TrimEnd('/');
                        if (displayName.Contains("/"))
                        {
                            displayName = displayName.Substring(displayName.LastIndexOf('/') + 1);
                        }
                        string nodeText = childItem.IsDirectory 
                            ? $"📁 {displayName}" 
                            : $"📄 {displayName} ({_fileService.FormatFileSize(childItem.Size)})";
                        var childNode = new TreeNode(nodeText) { Tag = childItem, Name = childItem.Key };

                        if (childItem.IsDirectory)
                        {
                            // Replicating S3FolderHasImmediateChildren's core logic here
                            string grandChildPrefixToCheck = childItem.Key;
                            if (!grandChildPrefixToCheck.EndsWith("/")) grandChildPrefixToCheck += "/";
                            
                            bool hasGrandChildren = false;
                            if (_s3Files != null) 
                            {
                                hasGrandChildren = _s3Files.Any(f => {
                                    if (!f.Key.StartsWith(grandChildPrefixToCheck) || f.Key == grandChildPrefixToCheck) return false;
                                    string remainder = f.Key.Substring(grandChildPrefixToCheck.Length);
                                    return !string.IsNullOrEmpty(remainder) && !remainder.TrimEnd('/').Contains("/");
                                });
                            }
                            if (hasGrandChildren)
                            {
                                childNode.Nodes.Add(new TreeNode("Loading..."));
                            }
                        }
                        node.Nodes.Add(childNode);
                    }

                    RestoreCheckedStates(node.Nodes, _s3CheckedItems, true); 
                    treeView.EndUpdate();
                    _isUpdatingTree = false;
                }
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
                    // string folderName = Path.GetFileName(_selectedLocalPath);

                    string targetS3Prefix = "";
                    var s3TreeView = this.Controls.Find("s3TreeView", true).FirstOrDefault() as TreeView;

                    List<S3FileItem> checkedS3Folders = new List<S3FileItem>();
                    if (_s3CheckedItems != null && _s3Files != null) // Ensure _s3Files is available
                    {
                        foreach (var kvp in _s3CheckedItems)
                        {
                            if (kvp.Value == true) // If checked
                            {
                                var s3Item = _s3Files.FirstOrDefault(f => f.Key == kvp.Key);
                                // If S3FileItem.IsDirectory is true, its Key must end with "/".
                                if (s3Item != null && s3Item.IsDirectory)
                                {
                                    checkedS3Folders.Add(s3Item);
                                }
                            }
                        }
                    }

                    if (checkedS3Folders.Count == 1)
                    {
                        var s3FolderItem = checkedS3Folders[0];
                        // s3FolderItem.IsDirectory is true, so s3FolderItem.Key already ends with "/"
                        targetS3Prefix = s3FolderItem.Key;
                    }
                    else if (checkedS3Folders.Count > 1)
                    {
                        MessageBox.Show("Please check only one S3 folder to use as the destination for the sync.", "Multiple S3 Folders Checked", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        progressForm.Close(); // Close progress form
                        return; // Abort sync
                    }
                    else // No S3 folders checked. Fallback to SelectedNode.
                    {
                        if (s3TreeView != null && s3TreeView.SelectedNode != null)
                        {
                            var s3Item = s3TreeView.SelectedNode.Tag as S3FileItem;
                            // If s3Item.IsDirectory is true, its Key must end with "/"
                            if (s3Item != null && s3Item.IsDirectory)
                            {
                                targetS3Prefix = s3Item.Key;
                            }
                        }
                    }

                    // Crucially: If, after these checks, targetS3Prefix is still empty
                    if (string.IsNullOrEmpty(targetS3Prefix))
                    {
                        MessageBox.Show("Please select or check a single S3 folder to be the destination.", "No Destination S3 Folder Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        progressForm.Close(); // Close progress form
                        return; // Abort sync
                    }
                    
                    // Ensure targetS3Prefix ends with a "/" if it's a non-empty prefix and represents a directory.
                    // S3FileItem.Key for directories should already end with "/", so this might be redundant but safe.
                    if (!string.IsNullOrEmpty(targetS3Prefix) && !targetS3Prefix.EndsWith("/"))
                    {
                        targetS3Prefix += "/";
                    }

                    await _s3Service.UploadDirectoryAsync(_selectedLocalPath, targetS3Prefix, roleForm.SelectedRoles);
                }
                else
                {
                    // New sync: S3 to Local
                    string s3SourcePrefix = "";
                    var s3TreeView = this.Controls.Find("s3TreeView", true).FirstOrDefault() as TreeView;

                    // Checked Folders Logic
                    if (_s3CheckedItems != null && _s3Files != null) // Ensure _s3Files is available for explicit checks
                    {
                        List<string> currentCheckedKeys = _s3CheckedItems.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();

                        if (currentCheckedKeys.Count == 1)
                        {
                            string checkedKey = currentCheckedKeys[0];
                            var s3ItemFromCheckedKey = _s3Files.FirstOrDefault(f => f.Key == checkedKey);

                            if (s3ItemFromCheckedKey != null && s3ItemFromCheckedKey.IsDirectory)
                            {
                                s3SourcePrefix = s3ItemFromCheckedKey.Key;
                            }
                            else if (s3ItemFromCheckedKey == null && checkedKey.EndsWith("/")) // Implicit folder
                            {
                                s3SourcePrefix = checkedKey;
                            }
                        }
                        else if (currentCheckedKeys.Count > 1)
                        {
                            MessageBox.Show("Please check only one S3 folder to use as the source for the sync.", "Multiple S3 Folders Checked", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            progressForm.Close();
                            return; // Abort sync
                        }
                    }

                    // Selected Node Logic (if no single valid folder was checked)
                    if (string.IsNullOrEmpty(s3SourcePrefix))
                    {
                        if (s3TreeView != null && s3TreeView.SelectedNode != null)
                        {
                            var selectedS3Item = s3TreeView.SelectedNode.Tag as S3FileItem;
                            if (selectedS3Item != null && selectedS3Item.IsDirectory)
                            {
                                s3SourcePrefix = selectedS3Item.Key;
                            }
                        }
                    }

                    // Final Validation (Crucial)
                    if (string.IsNullOrEmpty(s3SourcePrefix))
                    {
                        MessageBox.Show("Please select or check a single S3 folder to be the source for the sync. This folder's contents will be downloaded to your selected local path.", "No Source S3 Folder Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        progressForm.Close();
                        return; // Abort sync
                    }
                    
                    // Ensure s3SourcePrefix ends with a "/" if it's a non-empty prefix and represents a directory.
                    // This should generally be true if IsDirectory was true or key ended with "/" for implicit.
                    if (!string.IsNullOrEmpty(s3SourcePrefix) && !s3SourcePrefix.EndsWith("/"))
                    {
                        s3SourcePrefix += "/";
                    }

                    progressForm.UpdateMessage($"Downloading S3 files (from prefix '{s3SourcePrefix}') to local folder...");
                    List<string> extraLocalFiles = await SyncS3ToLocal(_selectedLocalPath, s3SourcePrefix, progressForm);
                    
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
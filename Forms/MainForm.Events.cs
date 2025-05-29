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
                                var pathParts = remainingPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
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
                        S3FileItem? childItem = _s3Files.FirstOrDefault(f => f.Key == childKey); // CS8600 addressed: childItem is S3FileItem?
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
                MessageBox.Show("Only administrators can sync folders.", "Access Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 1. Local Folder Identification
            if (string.IsNullOrEmpty(_selectedLocalPath))
            {
                MessageBox.Show("Please browse and select a valid local folder for synchronization.", "No Local Folder Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning); // Slightly refined
                return;
            }
            if (!Directory.Exists(_selectedLocalPath))
            {
                MessageBox.Show("The selected local folder no longer exists. Please browse and select a valid folder for synchronization.", "Invalid Local Folder", MessageBoxButtons.OK, MessageBoxIcon.Error); // Slightly refined
                return;
            }

            // 2. S3 Folder Identification
            string selectedS3FolderKey = "";
            var s3TreeView = this.Controls.Find("s3TreeView", true).FirstOrDefault() as TreeView;

            string selectedNodeCandidateKey = "";
            if (s3TreeView?.SelectedNode?.Tag is S3FileItem selectedS3Item && selectedS3Item.IsDirectory)
            {
                selectedNodeCandidateKey = selectedS3Item.Key;
            }

            string finalCheckedFolderCandidateKey = "";
            if (_s3CheckedItems != null && _s3Files != null)
            {
                List<string> allCheckedKeys = _s3CheckedItems.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();
                List<string> potentialTopMostCheckedFolderKeys = new List<string>();

                foreach (string key in allCheckedKeys)
                {
                    var s3Item = _s3Files.FirstOrDefault(f => f.Key == key);
                    bool isDirectoryCandidate = (s3Item != null && s3Item.IsDirectory) || (s3Item == null && key.EndsWith("/"));

                    if (isDirectoryCandidate)
                    {
                        bool isTopMost = !allCheckedKeys.Any(otherKey => key != otherKey && key.StartsWith(otherKey));
                        if (isTopMost)
                        {
                            potentialTopMostCheckedFolderKeys.Add(key);
                        }
                    }
                }
                potentialTopMostCheckedFolderKeys = potentialTopMostCheckedFolderKeys.Distinct().ToList();

                if (potentialTopMostCheckedFolderKeys.Count == 1)
                {
                    finalCheckedFolderCandidateKey = potentialTopMostCheckedFolderKeys[0];
                }
                else if (potentialTopMostCheckedFolderKeys.Count > 1)
                {
                    MessageBox.Show("Multiple distinct S3 folders are checked. Please check only one main S3 folder to be the target/source for synchronization.", "Ambiguous S3 Folders Checked", MessageBoxButtons.OK, MessageBoxIcon.Warning); // Refined
                    return;
                }
            }

            if (!string.IsNullOrEmpty(selectedNodeCandidateKey) && !string.IsNullOrEmpty(finalCheckedFolderCandidateKey))
            {
                if (selectedNodeCandidateKey != finalCheckedFolderCandidateKey)
                {
                    MessageBox.Show("The S3 folder selected in the tree conflicts with the primary S3 folder that is checked. Please clarify the intended S3 folder by ensuring only one is designated (either by selection or by a single top-most check).", "Conflicting S3 Folders", MessageBoxButtons.OK, MessageBoxIcon.Warning); // Refined
                    return;
                }
                selectedS3FolderKey = selectedNodeCandidateKey; // They are the same
            }
            else if (!string.IsNullOrEmpty(selectedNodeCandidateKey))
            {
                selectedS3FolderKey = selectedNodeCandidateKey;
            }
            else if (!string.IsNullOrEmpty(finalCheckedFolderCandidateKey))
            {
                selectedS3FolderKey = finalCheckedFolderCandidateKey;
            }

            if (string.IsNullOrEmpty(selectedS3FolderKey))
            {
                MessageBox.Show("Please select or check a single S3 folder to be the target/source for synchronization.", "No S3 Folder Designated", MessageBoxButtons.OK, MessageBoxIcon.Warning); // Refined
                return;
            }

            if (!selectedS3FolderKey.EndsWith("/"))
            {
                selectedS3FolderKey += "/"; // Ensure it's a prefix
            }

            // 3. Proceed to Sync Direction Dialog
            var syncForm = new SyncDirectionForm();
            if (syncForm.ShowDialog() != DialogResult.OK)
                return;

            ProgressForm? progressForm = null; // Declare here to be accessible in catch/finally
            try
            {
                progressForm = new ProgressForm("Syncing files...");
                progressForm.Show();

                if (syncForm.SyncDirection == SyncDirection.LocalToS3)
                {
                    var roleForm = new RoleSelectionForm();
                    if (roleForm.ShowDialog() != DialogResult.OK)
                    {
                        progressForm?.Close(); // Ensure progress form is closed
                        return;
                    }
                    progressForm.UpdateMessage("Uploading local files to S3...");
                    await _s3Service.UploadDirectoryAsync(_selectedLocalPath, selectedS3FolderKey, roleForm.SelectedRoles);
                    // No separate message for LocalToS3 success, will be handled by the generic success message after refresh
                }
                else // SyncDirection.S3ToLocal
                {
                    progressForm.UpdateMessage($"Downloading S3 files (from prefix '{selectedS3FolderKey}') to local folder...");
                    List<string> extraLocalFiles = await SyncS3ToLocal(_selectedLocalPath, selectedS3FolderKey, progressForm);

                    // Specific S3ToLocal handling with extra files message
                    progressForm.Close(); // Close before message
                    progressForm = null; // Indicate it's closed

                    if (extraLocalFiles != null && extraLocalFiles.Any())
                    {
                        string fileList = string.Join(Environment.NewLine, extraLocalFiles.Select(f => $"- {f}"));
                        string warningMessage = $"Sync complete. However, the following local files do not exist in the S3 bucket:{Environment.NewLine}{Environment.NewLine}{fileList}{Environment.NewLine}{Environment.NewLine}You may want to upload these files to S3 or remove them from your local folder if they are no longer needed.";
                        MessageBox.Show(warningMessage, "Sync Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    else
                    {
                        MessageBox.Show("Sync completed successfully! No extra local files found.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }

                // Generic success message if not S3ToLocal with its specific messages
                if (progressForm != null) // Was not closed by S3ToLocal logic
                {
                    progressForm.Close();
                    MessageBox.Show("Sync completed successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                await LoadS3FilesAsync();
                LoadLocalFiles(_selectedLocalPath);
            }
            catch (Exception ex)
            {
                progressForm?.Close(); // Ensure progress form is closed on error
                MessageBox.Show($"Error during sync: {ex.Message}", "Sync Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
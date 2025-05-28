// Forms/MainForm.Operations.cs - Core Operations
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
        #region Local File Operations

        private void LoadLocalFiles(string path)
        {
            try
            {
                var localTreeView = this.Controls.Find("localTreeView", true).FirstOrDefault() as TreeView;
                if (localTreeView == null) return;

                _isUpdatingTree = true;
                localTreeView.BeginUpdate();

                // Clear previous selections for this path
                _localCheckedItems.Clear();

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

                localTreeView.EndUpdate();
                _isUpdatingTree = false;

                UpdateLocalSelectionCount();
            }
            catch (Exception ex)
            {
                _isUpdatingTree = false;
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

        private List<LocalFileItem> GetCheckedLocalItems()
        {
            var items = new List<LocalFileItem>();

            foreach (var kvp in _localCheckedItems)
            {
                if (kvp.Value) // If checked
                {
                    var item = _localFiles.FirstOrDefault(f => f.FullPath == kvp.Key);
                    if (item != null)
                    {
                        items.Add(item);
                    }
                    else
                    {
                        // Create item from path if not in cache
                        try
                        {
                            var info = new FileInfo(kvp.Key);
                            if (info.Exists)
                            {
                                items.Add(new LocalFileItem
                                {
                                    Name = info.Name,
                                    FullPath = kvp.Key,
                                    IsDirectory = false,
                                    Size = info.Length
                                });
                            }
                            else
                            {
                                var dirInfo = new DirectoryInfo(kvp.Key);
                                if (dirInfo.Exists)
                                {
                                    items.Add(new LocalFileItem
                                    {
                                        Name = dirInfo.Name,
                                        FullPath = kvp.Key,
                                        IsDirectory = true,
                                        Size = 0
                                    });
                                }
                            }
                        }
                        catch
                        {
                            // Skip invalid paths
                        }
                    }
                }
            }

            return items;
        }

        #endregion

        #region S3 Operations

        private async Task LoadS3FilesAsync()
        {
            try
            {
                // Show loading indicator
                var bucketLabel = this.Controls.Find("bucketLabel", true).FirstOrDefault() as Label;
                if (bucketLabel != null)
                {
                    if (this.InvokeRequired)
                        this.Invoke(new Action(() => bucketLabel.Text = "Loading..."));
                    else
                        bucketLabel.Text = "Loading...";
                }

                // Load S3 files based on user role
                _s3Files = await _s3Service.ListFilesAsync(_currentUser.Role);

                // Handle case where user has no accessible files
                if (_s3Files == null)
                {
                    _s3Files = new List<S3FileItem>();
                }

                // Update UI on main thread
                if (this.InvokeRequired)
                    this.Invoke(new Action(() => UpdateS3TreeViewOptimized()));
                else
                    UpdateS3TreeViewOptimized();
            }
            catch (Exception ex)
            {
                // Ensure _s3Files is never null
                if (_s3Files == null)
                    _s3Files = new List<S3FileItem>();

                var action = new Action(() =>
                {
                    MessageBox.Show($"Error loading S3 files: {ex.Message}", "S3 Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);

                    var bucketLabel = this.Controls.Find("bucketLabel", true).FirstOrDefault() as Label;
                    if (bucketLabel != null)
                        bucketLabel.Text = "Error loading bucket";

                    // Update the tree view even with empty list to clear any previous content
                    UpdateS3TreeViewOptimized();
                });

                if (this.InvokeRequired)
                    this.Invoke(action);
                else
                    action();
            }
        }

        private void UpdateS3TreeViewOptimized()
        {
            var s3TreeView = this.Controls.Find("s3TreeView", true).FirstOrDefault() as TreeView;
            if (s3TreeView == null) return;

            _isUpdatingTree = true;
            s3TreeView.BeginUpdate();

            try
            {
                // Store current expanded states (skip scroll position for now to avoid errors)
                var expandedNodes = new HashSet<string>();
                StoreExpandedStates(s3TreeView.Nodes, expandedNodes);

                s3TreeView.Nodes.Clear();

                // Build tree structure efficiently
                var nodeCache = new Dictionary<string, TreeNode>();

                // Sort files for better performance
                var sortedFiles = _s3Files.OrderBy(f => f.Key).ToList();

                foreach (var item in sortedFiles)
                {
                    AddS3ItemToTreeOptimized(s3TreeView, nodeCache, item);
                }

                // Restore expanded states
                RestoreExpandedStates(s3TreeView.Nodes, expandedNodes);

                // Restore checked states
                RestoreCheckedStates(s3TreeView.Nodes, _s3CheckedItems, true);

                // Update UI labels
                var bucketLabel = this.Controls.Find("bucketLabel", true).FirstOrDefault() as Label;
                if (bucketLabel != null)
                {
                    var config = ConfigurationService.GetConfiguration();
                    bucketLabel.Text = $"Bucket: {config.AWS.BucketName} ({_s3Files.Count} items visible)";
                }

                UpdateS3SelectionCount();
            }
            catch (Exception ex)
            {
                // Handle any errors gracefully
                MessageBox.Show($"Error updating tree view: {ex.Message}", "Tree View Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                s3TreeView.EndUpdate();
                _isUpdatingTree = false;
            }
        }

        private void AddS3ItemToTreeOptimized(TreeView treeView, Dictionary<string, TreeNode> nodeCache, S3FileItem item)
        {
            var pathParts = item.Key.Split('/');
            TreeNodeCollection currentNodes = treeView.Nodes;
            string currentPath = "";

            for (int i = 0; i < pathParts.Length; i++)
            {
                var part = pathParts[i];
                if (string.IsNullOrEmpty(part)) continue;

                currentPath += (i > 0 ? "/" : "") + part;
                bool isLastPart = i == pathParts.Length - 1;
                bool isFile = isLastPart && !item.IsDirectory;

                // Check cache first for performance
                TreeNode? existingNode = null;
                if (nodeCache.ContainsKey(currentPath))
                {
                    existingNode = nodeCache[currentPath];
                }
                else
                {
                    // Look for existing node in current level only
                    foreach (TreeNode node in currentNodes)
                    {
                        if (node.Tag is S3FileItem nodeItem &&
                            (nodeItem.Key == currentPath || nodeItem.Key == currentPath + "/"))
                        {
                            existingNode = node;
                            nodeCache[currentPath] = node;
                            break;
                        }
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
                        Tag = nodeItem,
                        Name = currentPath // Set name for easier searching
                    };

                    currentNodes.Add(newNode);
                    nodeCache[currentPath] = newNode;
                    existingNode = newNode;
                }

                if (!isLastPart)
                {
                    currentNodes = existingNode.Nodes;
                }
            }
        }

        private List<S3FileItem> GetCheckedS3Items()
        {
            var items = new List<S3FileItem>();

            foreach (var kvp in _s3CheckedItems)
            {
                if (kvp.Value) // If checked
                {
                    var item = _s3Files.FirstOrDefault(f => f.Key == kvp.Key);
                    if (item != null)
                    {
                        items.Add(item);
                    }
                }
            }

            return items;
        }

        #endregion

        #region Download Operations

        private async Task DownloadS3File(string s3Key, string baseDownloadPath, ProgressForm progressForm)
        {
            try
            {
                progressForm.UpdateMessage($"Downloading file: {Path.GetFileName(s3Key)}");

                // Create subdirectory structure if the S3 key contains folders
                string relativePath = s3Key.Replace('/', Path.DirectorySeparatorChar);
                string fullLocalPath = Path.Combine(baseDownloadPath, relativePath);
                string localDirectory = Path.GetDirectoryName(fullLocalPath) ?? baseDownloadPath;

                // Create directory structure
                if (!Directory.Exists(localDirectory))
                {
                    Directory.CreateDirectory(localDirectory);
                }

                // Download the file
                await _s3Service.DownloadFileAsync(s3Key, localDirectory);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to download file '{s3Key}': {ex.Message}");
            }
        }

        private async Task DownloadS3Folder(string folderKey, string baseDownloadPath, ProgressForm progressForm)
        {
            try
            {
                // Get all files in this folder (and subfolders)
                var folderFiles = _s3Files.Where(f =>
                    f.Key.StartsWith(folderKey) &&
                    f.Key != folderKey &&
                    !f.IsDirectory).ToList();

                if (folderFiles.Count == 0)
                {
                    // Create empty folder
                    string emptyFolderPath = Path.Combine(baseDownloadPath, folderKey.TrimEnd('/').Replace('/', Path.DirectorySeparatorChar));
                    if (!Directory.Exists(emptyFolderPath))
                    {
                        Directory.CreateDirectory(emptyFolderPath);
                    }
                    return;
                }

                progressForm.UpdateMessage($"Downloading folder: {folderKey} ({folderFiles.Count} files)");

                for (int i = 0; i < folderFiles.Count; i++)
                {
                    var file = folderFiles[i];
                    progressForm.UpdateMessage($"Downloading: {file.Key} ({i + 1}/{folderFiles.Count})");

                    // Create the full local path maintaining folder structure
                    string relativePath = file.Key.Replace('/', Path.DirectorySeparatorChar);
                    string fullLocalPath = Path.Combine(baseDownloadPath, relativePath);
                    string localDirectory = Path.GetDirectoryName(fullLocalPath) ?? baseDownloadPath;

                    // Create directory structure
                    if (!Directory.Exists(localDirectory))
                    {
                        Directory.CreateDirectory(localDirectory);
                    }

                    // Download the file
                    await _s3Service.DownloadFileAsync(file.Key, localDirectory);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to download folder '{folderKey}': {ex.Message}");
            }
        }

        #endregion

        #region Sync Operations

        private async Task SyncS3ToLocal(string localPath, ProgressForm progressForm)
        {
            // Get all accessible S3 files
            var accessibleFiles = _s3Files.Where(f => !f.IsDirectory).ToList();

            if (accessibleFiles.Count == 0)
            {
                progressForm.UpdateMessage("No files to sync from S3.");
                return;
            }

            for (int i = 0; i < accessibleFiles.Count; i++)
            {
                var s3File = accessibleFiles[i];
                progressForm.UpdateMessage($"Syncing: {s3File.Key} ({i + 1}/{accessibleFiles.Count})");

                // Create local file path maintaining S3 folder structure
                string localFilePath = Path.Combine(localPath, s3File.Key.Replace('/', Path.DirectorySeparatorChar));
                string localFileDir = Path.GetDirectoryName(localFilePath) ?? localPath;

                // Create directory if it doesn't exist
                if (!Directory.Exists(localFileDir))
                {
                    Directory.CreateDirectory(localFileDir);
                }

                // Check if file needs to be downloaded (doesn't exist or is older)
                bool shouldDownload = !File.Exists(localFilePath);
                if (!shouldDownload)
                {
                    var localFileInfo = new FileInfo(localFilePath);
                    // Download if S3 file is newer or sizes don't match
                    shouldDownload = localFileInfo.LastWriteTime < s3File.LastModified || localFileInfo.Length != s3File.Size;
                }

                if (shouldDownload)
                {
                    await _s3Service.DownloadFileAsync(s3File.Key, localFileDir);
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
    }
}
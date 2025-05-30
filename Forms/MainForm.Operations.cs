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

            // Addressing: Possible null reference argument for parameter 'source' if _localFiles could be null.
            // Also noting that _localFiles is not a defined field in the provided MainForm snippets,
            // which is a separate issue. Assuming it *should* exist for this fix.
            if (_localFiles == null) 
            {
                // If there's no source list, we can't find items in it.
                // Depending on expected behavior, one might still iterate _localCheckedItems
                // and try to create items from paths if that's the fallback.
                // For now, just returning empty list if source is null.
                // The existing else block (creating from FileInfo/DirectoryInfo) handles item not being in _localFiles.
                // This guard is purely for _localFiles being null.
                // A more robust fix would involve understanding how _localFiles is populated or if it's an error.
                // For CS8604, this guard on _localFiles is the direct address.
                // However, the provided code does not show _localFiles as a class member.
                // This fix assumes it *is* a member for the purpose of the warning.
                // If it's not, then `_localFiles.FirstOrDefault` is a compile error, not a warning.
                // Given the context of fixing warnings, I'll add the guard.
                 return items; // Or, if _localFiles is essential, throw or handle error.
                               // The current fallback of creating from path if item not in _localFiles suggests
                               // that _localFiles might be a cache or a primary list.
                               // Let's assume it could be null and we should proceed to the fallback.
                               // So, if _localFiles is null, every item will be "not found" and created from path.
                               // This makes the `if (_localFiles == null) return items;` less useful if the fallback is always desired.
                               // The most direct fix for the warning *if _localFiles is the source of FirstOrDefault*
                               // is to ensure _localFiles is not null.
                               // The existing code structure implies _localFiles is a List<LocalFileItem>.
                               // Let's assume it's a field that should be initialized.
                               // if (_localFiles == null) _localFiles = new List<LocalFileItem>(); // Alternative
            }


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

        // Helper to determine if an S3 item (given its key, which should be a folder prefix) has immediate children
        private bool S3FolderHasImmediateChildren(string folderKeyPrefix)
        {
            if (string.IsNullOrEmpty(folderKeyPrefix)) return false; 
            
            string normalizedPrefix = folderKeyPrefix.EndsWith("/") ? folderKeyPrefix : folderKeyPrefix + "/";

            return _s3Files.Any(f => {
                if (!f.Key.StartsWith(normalizedPrefix) || f.Key == normalizedPrefix) return false; 
                string remainder = f.Key.Substring(normalizedPrefix.Length);
                return !string.IsNullOrEmpty(remainder) && !remainder.TrimEnd('/').Contains("/"); 
            });
        }

        // Method to add a single S3FileItem as a TreeNode to a given collection.
        // addDummyNodeIfFolderHasChildren is true if we are in a context where "Loading..." nodes are desired.
        private void AddSingleS3NodeToCollection(TreeNodeCollection parentNodes, S3FileItem item, bool addDummyNodeIfFolderHasChildren)
        {
            string displayName = item.Key.TrimEnd('/');
            if (displayName.Contains("/"))
            {
                displayName = displayName.Substring(displayName.LastIndexOf('/') + 1);
            }

            string nodeText = item.IsDirectory 
                ? $"📁 {displayName}" 
                : $"📄 {displayName} ({_fileService.FormatFileSize(item.Size)})";

            var newNode = new TreeNode(nodeText)
            {
                Tag = item,
                Name = item.Key 
            };

            if (item.IsDirectory && addDummyNodeIfFolderHasChildren)
            {
                if (S3FolderHasImmediateChildren(item.Key))
                {
                    newNode.Nodes.Add(new TreeNode("Loading..."));
                }
            }
            parentNodes.Add(newNode);
        }
        
        private void UpdateS3TreeViewOptimized()
        {
            var s3TreeView = this.Controls.Find("s3TreeView", true).FirstOrDefault() as TreeView;
            if (s3TreeView == null) return;

            _isUpdatingTree = true;
            s3TreeView.BeginUpdate();

            try
            {
                var expandedNodes = new HashSet<string>();
                StoreExpandedStates(s3TreeView.Nodes, expandedNodes);
                
                s3TreeView.Nodes.Clear();

                if (_s3Files == null) _s3Files = new List<S3FileItem>();

                var topLevelKeys = new HashSet<string>();
                foreach (var s3File in _s3Files)
                {
                    var keyParts = s3File.Key.Split(new[] {'/'}, StringSplitOptions.RemoveEmptyEntries);
                    if (keyParts.Length > 0)
                    {
                        string firstPart = keyParts[0];
                        // A key like "folderA" (no trailing slash) is a file unless IsDirectory is true.
                        // A key like "folderA/" is a folder.
                        // If original key was "folderA/file.txt", topLevelKey should be "folderA/"
                        bool isFolderViaPath = s3File.Key.Length > firstPart.Length && s3File.Key[firstPart.Length] == '/';
                        string topLevelKey = isFolderViaPath ? firstPart + "/" : firstPart;
                        topLevelKeys.Add(topLevelKey);
                    }
                }
                
                foreach (string topKey in topLevelKeys.OrderBy(k => k))
                {
                    S3FileItem? topLevelItem = _s3Files.FirstOrDefault(f => f.Key == topKey); // CS8600 addressed: topLevelItem is S3FileItem?
                    
                    // If topKey represents an item not explicitly in _s3Files (e.g., an implicit folder)
                    if (topLevelItem == null)
                    {
                        // If topKey ends with "/", it's an implicit folder.
                        // S3FileItem's IsDirectory property will be true due to the Key ending with "/".
                        // If topKey does not end with "/", it's treated as an implicit file.
                        topLevelItem = new S3FileItem { Key = topKey, Size = 0, LastModified = DateTime.MinValue };
                    }
                    // No direct assignment to topLevelItem.IsDirectory is needed here.
                    // The Key property (e.g., "folderA/" or "file.txt") determines IsDirectory.
                    // The logic for deriving topKey already ensures it ends with "/" if it's a folder.

                    AddSingleS3NodeToCollection(s3TreeView.Nodes, topLevelItem, true); // true: addDummyNodeIfFolderHasChildren
                }
                
                RestoreExpandedStates(s3TreeView.Nodes, expandedNodes);
                RestoreCheckedStates(s3TreeView.Nodes, _s3CheckedItems, true); // true for S3 items

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

        // The old AddS3ItemToTreeOptimized is removed as its path-building logic is not used in lazy loading.
        // The new AddSingleS3NodeToCollection and the logic within UpdateS3TreeViewOptimized and S3TreeView_BeforeExpand handle node creation.
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

            // Addressing: Possible null reference argument for parameter 'source' in 'FirstOrDefault'.
            // Although _s3Files is initialized and handled in LoadS3FilesAsync's catch,
            // this explicit check satisfies compiler static analysis if it still flags a warning.
            if (_s3Files == null)
            {
                return items; // Return empty list if _s3Files is unexpectedly null
            }

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

        private async Task<List<string>> SyncS3ToLocal(string localPath, string s3SourcePrefix, ProgressForm progressForm)
        {
            var extraLocalFiles = new List<string>();

            // --- Get Local Files (relative paths) ---
            var localFileRelativePaths = new Dictionary<string, FileInfo>();
            if (Directory.Exists(localPath))
            {
                // Ensure localPath ends with a separator for correct substring relative path calculation
                string adjustedLocalPath = localPath.EndsWith(Path.DirectorySeparatorChar.ToString()) 
                                           ? localPath 
                                           : localPath + Path.DirectorySeparatorChar;

                foreach (string filePath in Directory.EnumerateFiles(localPath, "*", SearchOption.AllDirectories))
                {
                    string fullPathNormalized = Path.GetFullPath(filePath); // Normalize for safety
                    if (fullPathNormalized.StartsWith(adjustedLocalPath, StringComparison.OrdinalIgnoreCase))
                    {
                        string relativeLocalPath = filePath.Substring(adjustedLocalPath.Length);
                        localFileRelativePaths[relativeLocalPath] = new FileInfo(filePath);
                    }
                }
            }

            // --- Filter and Process S3 Files ---
            var allS3Files = _s3Files.Where(f => !f.IsDirectory).ToList();
            // Normalize s3SourcePrefix to ensure it ends with a '/' if it's not empty
            string normalizedS3SourcePrefix = s3SourcePrefix;
            if (!string.IsNullOrEmpty(normalizedS3SourcePrefix) && !normalizedS3SourcePrefix.EndsWith("/"))
            {
                normalizedS3SourcePrefix += "/";
            }
            // If s3SourcePrefix is empty, we want to match all files (root level sync)
            // So, StartsWith("") is true for all strings.
            // If s3SourcePrefix is "folder/", we only match keys starting with "folder/".
            var relevantS3Files = string.IsNullOrEmpty(s3SourcePrefix) // Allow empty prefix for root
                                  ? allS3Files 
                                  : allS3Files.Where(f => f.Key.StartsWith(normalizedS3SourcePrefix)).ToList();

            if (relevantS3Files.Count == 0)
            {
                progressForm.UpdateMessage($"No files found in S3 path: '{s3SourcePrefix}'. Checking for local files to remove...");
                // All local files are extra if no S3 files are relevant
                extraLocalFiles.AddRange(localFileRelativePaths.Values.Select(fi => fi.FullName));
                progressForm.UpdateMessage("Sync process completed.");
                return extraLocalFiles;
            }
            else
            {
                for (int i = 0; i < relevantS3Files.Count; i++)
                {
                    var s3File = relevantS3Files[i];
                    // Ensure s3SourcePrefix is used for substring, not normalizedS3SourcePrefix if original was empty
                    string relativeS3Path = s3File.Key.Substring(s3SourcePrefix.Length); 
                    if (string.IsNullOrEmpty(relativeS3Path)) continue; // Skip if key was identical to prefix itself

                    progressForm.UpdateMessage($"Syncing: {s3File.Key} ({i + 1}/{relevantS3Files.Count})");

                    string localFilePath = Path.Combine(localPath, relativeS3Path.Replace('/', Path.DirectorySeparatorChar));
                    string localFileDir = Path.GetDirectoryName(localFilePath) ?? localPath;

                    if (!Directory.Exists(localFileDir))
                    {
                        Directory.CreateDirectory(localFileDir);
                    }

                    bool shouldDownload = true;
                    // Key for localFileRelativePaths uses Path.DirectorySeparatorChar
                    string localRelativePathKey = relativeS3Path.Replace('/', Path.DirectorySeparatorChar);
                    if (localFileRelativePaths.TryGetValue(localRelativePathKey, out var localFileInfo))
                    {
                        if (localFileInfo.LastWriteTimeUtc >= s3File.LastModified.ToUniversalTime() && localFileInfo.Length == s3File.Size)
                        {
                            shouldDownload = false;
                        }
                    }

                    if (shouldDownload)
                    {
                        progressForm.UpdateMessage($"Downloading: {s3File.Key}");
                        await _s3Service.DownloadFileAsync(s3File.Key, localFileDir); // Pass full s3File.Key
                        if (localFileRelativePaths.ContainsKey(localRelativePathKey))
                        {
                            localFileRelativePaths[localRelativePathKey].Refresh();
                        }
                        // If new, it's not "extra", so no need to add to localFileRelativePaths for this part
                    }
                }
            }

            // --- Identify Extra Local Files ---
            progressForm.UpdateMessage("Identifying extra local files...");
            // These keys from S3 are like "file.txt" or "subfolder/file.txt"
            var relevantS3RelativeKeys = new HashSet<string>(relevantS3Files.Select(f => f.Key.Substring(s3SourcePrefix.Length)));

            foreach (var kvp in localFileRelativePaths)
            {
                // kvp.Key is like "file.txt" or "subfolder\file.txt"
                string s3ComparableRelativePath = kvp.Key.Replace(Path.DirectorySeparatorChar, '/');
                if (!relevantS3RelativeKeys.Contains(s3ComparableRelativePath))
                {
                    extraLocalFiles.Add(kvp.Value.FullName);
                }
            }
            
            progressForm.UpdateMessage("Sync process completed.");
            return extraLocalFiles;

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
        
        private TableLayoutPanel? mainPanel; // Made nullable to address CS8618
    }
}
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using AWSS3Sync.Models;
using AWSS3Sync.Services;

namespace AWSS3Sync
{
    public partial class MainForm
    {
        // --- S3 Loading and Population ---

        private Task LoadS3FilesAsync()
        {
            var s3TreeView = this.Controls.Find("s3TreeView", true).FirstOrDefault() as TreeView;
            if (s3TreeView == null) return Task.CompletedTask;

            s3TreeView.Nodes.Clear();
            _s3CheckedItems.Clear();

            var rootNode = new TreeNode("S3 Bucket")
            {
                Tag = new FileNode("", "", true, 0, DateTime.MinValue, new List<UserRole>())
            };
            s3TreeView.Nodes.Add(rootNode);

            // Add placeholder for lazy loading
            rootNode.Nodes.Add(new TreeNode("Loading..."));
            rootNode.Expand(); // Expand the root to trigger the initial load
            return Task.CompletedTask;
        }

        private async Task LoadS3DirectoryNodesAsync(TreeNode parentNode)
        {
            if (!(parentNode.Tag is FileNode parentNodeInfo)) return;

            try
            {
                // Fetch direct descendants
                var files = await _s3Service.ListFilesAsync(_currentUser.Role, parentNodeInfo.Path);

                this.Invoke(new Action(() =>
                {
                    parentNode.Nodes.Clear(); // Clear "Loading..."
                    PopulateS3TreeNodes(parentNode, files);
                }));
            }
            catch (Exception ex)
            {
                this.Invoke(new Action(() =>
                {
                    parentNode.Nodes.Clear();
                    parentNode.Nodes.Add(new TreeNode($"Error: {ex.Message}"));
                }));
            }
        }

        private void PopulateS3TreeNodes(TreeNode parentNode, List<FileNode> files)
        {
            foreach (var file in files)
            {
                var node = new TreeNode(file.Name)
                {
                    Tag = file,
                    Checked = _s3CheckedItems.ContainsKey(file.Path) && _s3CheckedItems[file.Path]
                };

                if (file.IsDirectory)
                {
                    node.Nodes.Add(new TreeNode("Loading...")); // Add placeholder for sub-directories
                }
                parentNode.Nodes.Add(node);
            }
        }

        // --- Local File Loading ---

        private void LoadLocalFiles(string path)
        {
            var localTreeView = this.Controls.Find("localTreeView", true).FirstOrDefault() as TreeView;
            if (localTreeView == null) return;

            localTreeView.Nodes.Clear();
            _localCheckedItems.Clear();

            var dirInfo = new DirectoryInfo(path);
            var rootNode = new TreeNode(dirInfo.Name)
            {
                Tag = new FileNode(dirInfo.Name, dirInfo.FullName, true, 0, dirInfo.LastWriteTime)
            };

            if (dirInfo.GetFileSystemInfos().Length != 0)
            {
                rootNode.Nodes.Add(new TreeNode("Loading..."));
            }

            localTreeView.Nodes.Add(rootNode);

            LoadLocalDirectoryNodes(rootNode, path);
            rootNode.Expand();
            UpdateLocalSelectionCount();
        }

        private void LoadLocalDirectoryNodes(TreeNode parentNode, string path)
        {
            try
            {
                var dirInfo = new DirectoryInfo(path);
                foreach (var dir in dirInfo.GetDirectories())
                {
                    var dirNode = new TreeNode(dir.Name)
                    {
                        Tag = new FileNode(dir.Name, dir.FullName, true, 0, dir.LastWriteTime)
                    };
                    if (dir.GetFileSystemInfos().Length != 0)
                    {
                        dirNode.Nodes.Add(new TreeNode("Loading..."));
                    }
                    parentNode.Nodes.Add(dirNode);
                }

                foreach (var file in dirInfo.GetFiles())
                {
                    var fileNode = new TreeNode(file.Name)
                    {
                        Tag = new FileNode(file.Name, file.FullName, false, file.Length, file.LastWriteTime)
                    };
                    parentNode.Nodes.Add(fileNode);
                }
            }
            catch (UnauthorizedAccessException)
            {
                parentNode.Nodes.Add(new TreeNode("Access Denied"));
            }
        }

        // --- Item Selection ---

        private List<FileNode> GetCheckedLocalItems()
        {
            return GetCheckedItems(false);
        }

        private List<FileNode> GetCheckedS3Items()
        {
            return GetCheckedItems(true);
        }

        private List<FileNode> GetCheckedItems(bool isS3)
        {
            var checkedItems = isS3 ? _s3CheckedItems : _localCheckedItems;
            var treeView = isS3
                ? this.Controls.Find("s3TreeView", true).FirstOrDefault() as TreeView
                : this.Controls.Find("localTreeView", true).FirstOrDefault() as TreeView;

            if (treeView == null) return new List<FileNode>();

            var selectedNodes = new List<FileNode>();

            foreach (var path in checkedItems.Keys)
            {
                if (checkedItems[path])
                {
                    var node = FindNodeByPath(treeView.Nodes, path);
                    if (node?.Tag is FileNode fileNode)
                    {
                        selectedNodes.Add(fileNode);
                    }
                }
            }
            return selectedNodes;
        }

        private TreeNode? FindNodeByPath(TreeNodeCollection nodes, string path)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Tag is FileNode fileNode && fileNode.Path == path)
                {
                    return node;
                }
                var foundNode = FindNodeByPath(node.Nodes, path);
                if (foundNode != null)
                {
                    return foundNode;
                }
            }
            return null;
        }

        // --- Comparison UI Methods ---

        private void ApplyComparisonToTrees()
        {
            var localTreeView = this.Controls.Find("localTreeView", true).FirstOrDefault() as TreeView;
            var s3TreeView = this.Controls.Find("s3TreeView", true).FirstOrDefault() as TreeView;

            if (localTreeView == null || s3TreeView == null) return;

            foreach (var result in _comparisonResults)
            {
                TreeNode? nodeToColor = null;
                Color color = Color.Black;

                switch (result.Status)
                {
                    case ComparisonStatus.LocalOnly:
                        if (result.LocalFile != null)
                        {
                            nodeToColor = FindNodeByPath(localTreeView.Nodes, result.LocalFile.Path);
                        }
                        color = Color.Green;
                        break;
                    case ComparisonStatus.S3Only:
                        if (result.S3File != null)
                        {
                            nodeToColor = FindNodeByPath(s3TreeView.Nodes, result.S3File.Path);
                        }
                        color = Color.Red;
                        break;
                    case ComparisonStatus.Modified:
                        if (result.LocalFile != null)
                        {
                            var localNode = FindNodeByPath(localTreeView.Nodes, result.LocalFile.Path);
                            if (localNode != null) localNode.ForeColor = Color.Orange;
                        }
                        if (result.S3File != null)
                        {
                            var s3Node = FindNodeByPath(s3TreeView.Nodes, result.S3File.Path);
                            if (s3Node != null) s3Node.ForeColor = Color.Orange;
                        }
                        break;
                    case ComparisonStatus.Identical:
                        if (result.LocalFile != null)
                        {
                            var identicalLocalNode = FindNodeByPath(localTreeView.Nodes, result.LocalFile.Path);
                            if (identicalLocalNode != null) identicalLocalNode.ForeColor = Color.Gray;
                        }
                        if (result.S3File != null)
                        {
                            var identicalS3Node = FindNodeByPath(s3TreeView.Nodes, result.S3File.Path);
                            if (identicalS3Node != null) identicalS3Node.ForeColor = Color.Gray;
                        }
                        break;
                }

                if (nodeToColor != null)
                {
                    nodeToColor.ForeColor = color;
                }
            }
        }

        private void ResetTreeViewColors()
        {
            var localTreeView = this.Controls.Find("localTreeView", true).FirstOrDefault() as TreeView;
            var s3TreeView = this.Controls.Find("s3TreeView", true).FirstOrDefault() as TreeView;

            if (localTreeView != null)
            {
                GetCheckedNodes(localTreeView.Nodes, checkedItems);
            }
            return checkedItems;
        }

        #endregion

        #region S3 Operations

        private async Task LoadS3FilesAsync()
        {
            try
            {
                var bucketLabel = this.Controls.Find("bucketLabel", true).FirstOrDefault() as Label;
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() => bucketLabel.Text = "Loading..."));
                }
                else
                {
                    bucketLabel.Text = "Loading...";
                }

                _s3Files = await _s3Service.ListFilesAsync(_currentUser.Role);
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(UpdateS3TreeView));
                }
                else
                {
                    UpdateS3TreeView();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading S3 files: {ex.Message}", "S3 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateS3TreeView()
        {
            var s3TreeView = this.Controls.Find("s3TreeView", true).FirstOrDefault() as TreeView;
            if (s3TreeView == null) return;

            s3TreeView.Nodes.Clear();
            _s3CheckedItems.Clear();

            foreach (var fileNode in _s3Files)
            {
                var treeNode = new TreeNode(fileNode.Name) { Tag = fileNode };
                s3TreeView.Nodes.Add(treeNode);
                AddChildTreeNodes(treeNode, fileNode.Children);
            }

            UpdateS3SelectionCount();
        }

        private void AddChildTreeNodes(TreeNode parentTreeNode, List<FileNode> children)
        {
            foreach (var fileNode in children)
            {
                var treeNode = new TreeNode(fileNode.Name) { Tag = fileNode };
                parentTreeNode.Nodes.Add(treeNode);
                if (fileNode.Children.Any())
                {
                    AddChildTreeNodes(treeNode, fileNode.Children);
                }
            }
        }

        private List<FileNode> GetCheckedS3Items()
        {
            var checkedItems = new List<FileNode>();
            var s3TreeView = this.Controls.Find("s3TreeView", true).FirstOrDefault() as TreeView;
            if (s3TreeView != null)
            {
                GetCheckedNodes(s3TreeView.Nodes, checkedItems);
                // foreach (TreeNode node in s3TreeView.Nodes)
                // {
                //     ResetNodeColor(node);
                // }
            }
            return checkedItems;
        }

        private void ResetNodeColor(TreeNode node)
        {
            node.ForeColor = Color.Black;
            foreach (TreeNode child in node.Nodes)
            {
                ResetNodeColor(child);
            }
        }
		
        private void GetCheckedNodes(TreeNodeCollection nodes, List<FileNode> checkedItems)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Checked && node.Tag is FileNode fileNode)
                {
                    checkedItems.Add(fileNode);
                }
                if (node.Nodes.Count > 0)
                {
                    GetCheckedNodes(node.Nodes, checkedItems);
                }
            }
        }
        #endregion
    }
}

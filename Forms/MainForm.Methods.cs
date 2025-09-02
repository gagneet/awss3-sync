using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using S3FileManager.Models;

namespace S3FileManager
{
    public partial class MainForm
    {
        // --- S3 Loading and Population ---

        private async Task LoadS3FilesAsync()
        {
            var s3TreeView = this.Controls.Find("s3TreeView", true).FirstOrDefault() as TreeView;
            if (s3TreeView == null) return;

            s3TreeView.Nodes.Clear();
            _s3CheckedItems.Clear();

            var rootNode = new TreeNode("S3 Bucket")
            {
                Tag = new FileNode { IsDirectory = true, Path = "" }
            };
            s3TreeView.Nodes.Add(rootNode);

            // Add placeholder for lazy loading
            rootNode.Nodes.Add(new TreeNode("Loading..."));
            rootNode.Expand(); // Expand the root to trigger the initial load
        }

        private async Task LoadS3DirectoryNodesAsync(TreeNode parentNode)
        {
            if (!(parentNode.Tag is FileNode parentNodeInfo)) return;

            try
            {
                // Fetch direct descendants
                var files = await _s3Service.ListFilesAsync(parentNodeInfo.Path, _currentUser.Role);

                this.Invoke(new Action(() =>
                {
                    parentNode.Nodes.Clear(); // Clear "Loading..."
                    PopulateS3TreeNodes(parentNode, files);
                    UpdateS3SelectionCount(); // Recalculate selections
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

            var rootNode = new TreeNode(path)
            {
                Tag = new FileNode { Name = path, Path = path, IsDirectory = true }
            };
            localTreeView.Nodes.Add(rootNode);

            LoadLocalDirectoryNodes(rootNode, path);
            rootNode.Expand();
        }

        private void LoadLocalDirectoryNodes(TreeNode parentNode, string path)
        {
            try
            {
                // Add files
                var files = Directory.GetFiles(path).Select(f => new FileInfo(f));
                foreach (var file in files)
                {
                    var fileNode = new TreeNode(file.Name)
                    {
                        Tag = new FileNode
                        {
                            Name = file.Name,
                            Path = file.FullName,
                            Size = file.Length,
                            LastModified = file.LastWriteTimeUtc,
                            IsS3 = false
                        }
                    };
                    parentNode.Nodes.Add(fileNode);
                }

                // Add directories
                var directories = Directory.GetDirectories(path).Select(d => new DirectoryInfo(d));
                foreach (var dir in directories)
                {
                    var dirNode = new TreeNode(dir.Name)
                    {
                        Tag = new FileNode { Name = dir.Name, Path = dir.FullName, IsDirectory = true, IsS3 = false }
                    };
                    dirNode.Nodes.Add(new TreeNode("Loading...")); // Placeholder for lazy loading
                    parentNode.Nodes.Add(dirNode);
                }
            }
            catch (UnauthorizedAccessException)
            {
                parentNode.Nodes.Add(new TreeNode("Access Denied"));
            }
        }

        // --- Item Selection and Counting ---

        private List<FileNode> GetCheckedLocalItems()
        {
            return GetCheckedItems(true);
        }

        private List<FileNode> GetCheckedS3Items()
        {
            return GetCheckedItems(false);
        }

        private List<FileNode> GetCheckedItems(bool isLocal)
        {
            var checkedItems = isLocal ? _localCheckedItems : _s3CheckedItems;
            var treeView = isLocal
                ? this.Controls.Find("localTreeView", true).FirstOrDefault() as TreeView
                : this.Controls.Find("s3TreeView", true).FirstOrDefault() as TreeView;

            if (treeView == null) return new List<FileNode>();

            var selectedNodes = new List<FileNode>();

            // This method now directly uses the cached dictionary, which is much faster.
            foreach (var path in checkedItems.Keys)
            {
                if (checkedItems[path])
                {
                    // We need to find the node in the tree to get the full FileNode object
                    var node = FindNodeByPath(treeView.Nodes, path);
                    if (node?.Tag is FileNode fileNode)
                    {
                        selectedNodes.Add(fileNode);
                    }
                }
            }
            return selectedNodes;
        }

        private TreeNode FindNodeByPath(TreeNodeCollection nodes, string path)
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

        private void UpdateLocalSelectionCount()
        {
            var count = _localCheckedItems.Count(kv => kv.Value);
            var selectionLabel = this.Controls.Find("localSelectionLabel", true).FirstOrDefault() as Label;
            if (selectionLabel != null)
            {
                selectionLabel.Text = $"Selected: {count} items";
            }
        }

        private void UpdateS3SelectionCount()
        {
            var count = _s3CheckedItems.Count(kv => kv.Value);
            var selectionLabel = this.Controls.Find("s3SelectionLabel", true).FirstOrDefault() as Label;
            if (selectionLabel != null)
            {
                selectionLabel.Text = $"Selected: {count} items";
            }
        }

        // --- Comparison UI Methods ---

        private void ApplyComparisonToTrees()
        {
            var localTreeView = this.Controls.Find("localTreeView", true).FirstOrDefault() as TreeView;
            var s3TreeView = this.Controls.Find("s3TreeView", true).FirstOrDefault() as TreeView;

            if (localTreeView == null || s3TreeView == null) return;

            foreach (var result in _comparisonResults)
            {
                TreeNode nodeToColor = null;
                Color color = Color.Black;

                switch (result.Status)
                {
                    case ComparisonStatus.LocalOnly:
                        nodeToColor = FindNodeByPath(localTreeView.Nodes, result.LocalFile.Path);
                        color = Color.Green;
                        break;
                    case ComparisonStatus.S3Only:
                        nodeToColor = FindNodeByPath(s3TreeView.Nodes, result.S3File.Path);
                        color = Color.Red;
                        break;
                    case ComparisonStatus.Modified:
                        var localNode = FindNodeByPath(localTreeView.Nodes, result.LocalFile.Path);
                        if (localNode != null) localNode.ForeColor = Color.Orange;
                        var s3Node = FindNodeByPath(s3TreeView.Nodes, result.S3File.Path);
                        if (s3Node != null) s3Node.ForeColor = Color.Orange;
                        break;
                    case ComparisonStatus.Identical:
                        // Optionally color identical files to show they were part of the compare
                        var identicalLocalNode = FindNodeByPath(localTreeView.Nodes, result.LocalFile.Path);
                        if (identicalLocalNode != null) identicalLocalNode.ForeColor = Color.Gray;
                        var identicalS3Node = FindNodeByPath(s3TreeView.Nodes, result.S3File.Path);
                        if (identicalS3Node != null) identicalS3Node.ForeColor = Color.Gray;
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
                foreach (TreeNode node in localTreeView.Nodes)
                {
                    ResetNodeColor(node);
                }
            }
            if (s3TreeView != null)
            {
                foreach (TreeNode node in s3TreeView.Nodes)
                {
                    ResetNodeColor(node);
                }
            }
        }

        private void ResetNodeColor(TreeNode node)
        {
            node.ForeColor = Color.Black;
            foreach (TreeNode child in node.Nodes)
            {
                ResetNodeColor(child);
            }
        }
    }
}

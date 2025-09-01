using System;
using System.Collections.Generic;
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
            rootNode.Expand();
            UpdateLocalSelectionCount();
        }

        private void LoadLocalDirectoryNodes(TreeNode parentNode, string directoryPath)
        {
            try
            {
                var dirInfo = new DirectoryInfo(directoryPath);
                foreach (var dir in dirInfo.GetDirectories())
                {
                    var dirNode = new TreeNode(dir.Name)
                    {
                        Tag = new FileNode(dir.Name, dir.FullName, true, 0, dir.LastWriteTime)
                    };
                    if (dir.GetFileSystemInfos().Any())
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
            catch (Exception ex)
            {
                MessageBox.Show($"Error accessing directory: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private List<FileNode> GetCheckedLocalItems()
        {
            var checkedItems = new List<FileNode>();
            var localTreeView = this.Controls.Find("localTreeView", true).FirstOrDefault() as TreeView;
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
            }
            return checkedItems;
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

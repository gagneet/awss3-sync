using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using S3FileManager.Models;

namespace S3FileManager
{
    public partial class MainForm
    {
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

        private void S3TreeView_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            if (e.Node.Nodes.Count > 0 && e.Node.Nodes[0].Text == "Loading...")
            {
                e.Node.Nodes.Clear();
                if (e.Node.Tag is FileNode parentNode)
                {
                    var childItems = _s3Files
                        .Where(f => f.Key.StartsWith(parentNode.Path) && f.Key != parentNode.Path)
                        .Select(f => new { file = f, relativePath = f.Key.Substring(parentNode.Path.Length) })
                        .Where(x => !x.relativePath.TrimEnd('/').Contains('/'))
                        .ToList();

                    var prefixes = _s3Files
                        .Where(f => f.Key.StartsWith(parentNode.Path) && f.Key != parentNode.Path)
                        .Select(f => f.Key.Substring(parentNode.Path.Length).Split('/'))
                        .Where(p => p.Length > 1)
                        .Select(p => parentNode.Path + p[0] + "/")
                        .Distinct()
                        .ToList();

                    foreach (var item in childItems)
                    {
                        var childNode = new TreeNode(item.file.DisplayName)
                        {
                            Tag = new FileNode(item.file.DisplayName, item.file.Key, item.file.IsDirectory, item.file.Size, item.file.LastModified, item.file.AccessRoles)
                        };
                        e.Node.Nodes.Add(childNode);
                    }

                    foreach (var prefix in prefixes)
                    {
                        if (!childItems.Any(i => i.file.Key == prefix))
                        {
                            var displayName = prefix.TrimEnd('/').Split('/').Last();
                            var childNode = new TreeNode(displayName)
                            {
                                Tag = new FileNode(displayName, prefix, true, 0, DateTime.MinValue, new List<UserRole>())
                            };
                            if (_s3Files.Any(f => f.Key.StartsWith(prefix) && f.Key != prefix))
                            {
                                childNode.Nodes.Add(new TreeNode("Loading..."));
                            }
                            e.Node.Nodes.Add(childNode);
                        }
                    }
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
    }
}

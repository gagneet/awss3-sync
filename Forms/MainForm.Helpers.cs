// Forms/MainForm.Helpers.cs - Helper Methods
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using AWSS3Sync.Models;

namespace AWSS3Sync
{
    public partial class MainForm
    {
        #region Selection Management Helpers

        private void SetChildrenChecked(TreeNode parentNode, bool isChecked)
        {
            foreach (TreeNode childNode in parentNode.Nodes)
            {
                childNode.Checked = isChecked;

                // Update the tracking dictionary
                if (childNode.Tag is LocalFileItem localItem)
                {
                    _localCheckedItems[localItem.FullPath] = isChecked;
                }
                else if (childNode.Tag is S3FileItem s3Item)
                {
                    _s3CheckedItems[s3Item.Key] = isChecked;
                }

                // Recursively check children
                if (childNode.Nodes.Count > 0)
                {
                    SetChildrenChecked(childNode, isChecked);
                }
            }
        }

        private void RestoreCheckedStates(TreeNodeCollection nodes, Dictionary<string, bool> checkedItems, bool isS3)
        {
            foreach (TreeNode node in nodes)
            {
                string key = "";
                if (isS3 && node.Tag is S3FileItem s3Item)
                {
                    key = s3Item.Key;
                }
                else if (!isS3 && node.Tag is LocalFileItem localItem)
                {
                    key = localItem.FullPath;
                }

                if (!string.IsNullOrEmpty(key) && checkedItems.ContainsKey(key))
                {
                    node.Checked = checkedItems[key];
                }

                // Recursively restore for children
                if (node.Nodes.Count > 0)
                {
                    RestoreCheckedStates(node.Nodes, checkedItems, isS3);
                }
            }
        }

        private void UpdateLocalSelectionCount()
        {
            var count = _localCheckedItems.Values.Count(v => v);
            var label = this.Controls.Find("localSelectionLabel", true).FirstOrDefault() as Label;
            if (label != null)
            {
                label.Text = $"Selected: {count} items";
            }
        }

        private void UpdateS3SelectionCount()
        {
            var count = _s3CheckedItems.Values.Count(v => v);
            var label = this.Controls.Find("s3SelectionLabel", true).FirstOrDefault() as Label;
            if (label != null)
            {
                label.Text = $"Selected: {count} items";
            }
        }

        #endregion

        #region TreeView State Management

        private void StoreExpandedStates(TreeNodeCollection nodes, HashSet<string> expandedNodes)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.IsExpanded && node.Tag is S3FileItem item)
                {
                    expandedNodes.Add(item.Key);
                }

                if (node.Nodes.Count > 0)
                {
                    StoreExpandedStates(node.Nodes, expandedNodes);
                }
            }
        }

        private void RestoreExpandedStates(TreeNodeCollection nodes, HashSet<string> expandedNodes)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Tag is S3FileItem item && expandedNodes.Contains(item.Key))
                {
                    node.Expand();
                }

                if (node.Nodes.Count > 0)
                {
                    RestoreExpandedStates(node.Nodes, expandedNodes);
                }
            }
        }

        private int GetTreeViewScrollPosition(TreeView treeView)
        {
            try
            {
                // Check if TreeView is valid and handle is created
                if (treeView == null || treeView.IsDisposed || !treeView.IsHandleCreated)
                    return 0;

                return SendMessage(treeView.Handle, TVM_GETSCROLLPOS, 0, 0);
            }
            catch
            {
                return 0;
            }
        }

        private void SetTreeViewScrollPosition(TreeView treeView, int position)
        {
            try
            {
                // Check if TreeView is valid and handle is created
                if (treeView == null || treeView.IsDisposed || !treeView.IsHandleCreated || position == 0)
                    return;

                // Use a timer to delay the scroll position setting to ensure TreeView is fully loaded
                var timer = new System.Windows.Forms.Timer();
                timer.Interval = 50; // Small delay
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    timer.Dispose();

                    try
                    {
                        if (!treeView.IsDisposed && treeView.IsHandleCreated)
                        {
                            SendMessage(treeView.Handle, TVM_SETSCROLLPOS, 0, position);
                        }
                    }
                    catch
                    {
                        // Ignore scroll position errors
                    }
                };
                timer.Start();
            }
            catch
            {
                // Ignore if unable to set scroll position
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        private const int TVM_SETSCROLLPOS = 0x1100 + 63;
        private const int TVM_GETSCROLLPOS = 0x1100 + 62;

        #endregion

        #region Search and Filter Helpers

        private void FilterS3Tree(string searchTerm)
        {
            var s3TreeView = this.Controls.Find("s3TreeView", true).FirstOrDefault() as TreeView;
            if (s3TreeView == null) return;

            s3TreeView.BeginUpdate();

            try
            {
                if (string.IsNullOrEmpty(searchTerm))
                {
                    // Show all nodes
                    ShowAllNodes(s3TreeView.Nodes);
                }
                else
                {
                    // Hide nodes that don't match search
                    FilterNodes(s3TreeView.Nodes, searchTerm);
                }

                // Restore checked states after filtering
                RestoreCheckedStates(s3TreeView.Nodes, _s3CheckedItems, true);
            }
            finally
            {
                s3TreeView.EndUpdate();
            }
        }

        private bool FilterNodes(TreeNodeCollection nodes, string searchTerm)
        {
            bool hasVisibleChildren = false;

            foreach (TreeNode node in nodes)
            {
                bool nodeMatches = node.Text.ToLowerInvariant().Contains(searchTerm);
                bool hasMatchingChildren = node.Nodes.Count > 0 && FilterNodes(node.Nodes, searchTerm);

                bool shouldShow = nodeMatches || hasMatchingChildren;

                // Show/hide the node
                if (shouldShow)
                {
                    node.BackColor = nodeMatches ? Color.LightYellow : Color.Transparent;
                    node.ForeColor = Color.Black;
                    hasVisibleChildren = true;

                    if (hasMatchingChildren)
                    {
                        node.Expand();
                    }
                }
                else
                {
                    node.BackColor = Color.Transparent;
                    node.ForeColor = Color.LightGray;
                }
            }

            return hasVisibleChildren;
        }

        private void ShowAllNodes(TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes)
            {
                node.BackColor = Color.Transparent;
                node.ForeColor = Color.Black;

                if (node.Nodes.Count > 0)
                {
                    ShowAllNodes(node.Nodes);
                }
            }
        }

        #endregion
    }
}
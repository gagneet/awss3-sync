using Amazon.S3; // Keep for S3CannedACL if used, or pass as string
using Amazon.S3.Model; // Keep for S3 specific exceptions if caught
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
// Add using for the new service
using AWSS3Sync.Core.S3;
using AWSS3Sync.Core.Utils; // For Misc class
using AWSS3Sync.Core.Model; // Added for AppConstants

namespace AWSS3Sync.UI // Updated namespace
{
    public partial class frmS3Sync : Form
    {
        private readonly S3Service _s3Service; // New S3 service instance
        private string selectedFolderPath;
        private List<string> filesToUpload = new List<string>();
        private string _currentUserRole = AWSS3Sync.Core.Model.AppConstants.RoleAdmin; // ADD THIS LINE

        public frmS3Sync()
        {
            InitializeComponent();
            // Initialize the S3 service
            try
            {
                _s3Service = new S3Service();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize S3 Service: {ex.Message}", "Initialization Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                // Consider disabling UI elements or closing the form if initialization fails
                // For simplicity, we'll let it proceed, but operations will likely fail.
                // A more robust app might close or disable functionality.
                btnBrowseFolder.Enabled = false;
                btnSyncFolder.Enabled = false;
                btnUploadFile.Enabled = false;
                btnUploadFolder.Enabled = false;
                btnListS3Files.Enabled = false;
                btnDownloadFiles.Enabled = false;
                btnMoveToBackup.Enabled = false; // Assuming btnMoveToBackup is the intended control name
                if (this.Controls.ContainsKey("btnDeleteFiles")) (this.Controls["btnDeleteFiles"] as Button).Enabled = false; // If btnDeleteFiles is used
                if (this.Controls.ContainsKey("btnManageRoles")) (this.Controls["btnManageRoles"] as Button).Enabled = false;
            }
        }

        public virtual void InitializeForm()
        {
            // Implement any common initialization logic here
        }

        private void btnSelectFolder_Click(object sender, EventArgs e)
        {
            btnSyncFolder.Enabled = false;
            btnUploadFile.Enabled = false;
            btnUploadFolder.Enabled = false;

            using (var folderBrowserDialog = new FolderBrowserDialog())
            {
                if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
                {
                    selectedFolderPath = folderBrowserDialog.SelectedPath;
                    lblSourceFileName.Text = selectedFolderPath; // Update label if it's meant to show selected folder path
                    filesToUpload = Directory.GetFiles(selectedFolderPath, "*", SearchOption.AllDirectories).ToList();

                    lstLocalFilesBox.Items.Clear();
                    foreach (string filePath in filesToUpload)
                    {
                        lstLocalFilesBox.Items.Add(filePath);
                    }
                }
            }
            btnSyncFolder.Enabled = filesToUpload.Any();
            btnUploadFolder.Enabled = filesToUpload.Any();
            btnUploadFile.Enabled = true; // Or based on some other condition
        }

        private async void btnSyncFiles_Click(object sender, EventArgs e)
        {
            if (_s3Service == null) { MessageBox.Show("S3 Service not initialized.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
            if (string.IsNullOrEmpty(selectedFolderPath) || !filesToUpload.Any())
            {
                MessageBox.Show("Please select a folder and ensure there are files to sync.", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnUploadFolder.Enabled = false;
            btnUploadFile.Enabled = false;
            btnSyncFolder.Enabled = false;

            try
            {
                int days = 60; 
                var objectTags = new List<Tag>();
                if (this.chkGrantUserRoleAccess.Checked)
                {
                    objectTags.Add(new Tag { Key = AppConstants.AppRoleTagKey, Value = AppConstants.RoleUser });
                }
                else
                {
                    objectTags.Add(new Tag { Key = AppConstants.AppRoleTagKey, Value = AppConstants.RoleAdmin });
                }
                
                // TODO: This call needs S3Service.SyncLocalFilesToS3Async to be updated to accept 'objectTags'
                // Similar to UploadFolderContentsAsync, this will be handled by modifying the S3Service method later.
                await _s3Service.SyncLocalFilesToS3Async(filesToUpload, selectedFolderPath, days, objectTags: objectTags);
                MessageBox.Show("Folder synchronization completed successfully!", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (AmazonS3Exception s3Ex)
            {
                MessageBox.Show($"Error during S3 operation: {s3Ex.Message}", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An unexpected error occurred: {ex.Message}", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnUploadFile.Enabled = true;
                btnSyncFolder.Enabled = true; // Re-enable based on your logic
            }
        }

        private async void btnSyncFolder_Click(object sender, EventArgs e)
        {
            if (_s3Service == null) { MessageBox.Show("S3 Service not initialized.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
            if (string.IsNullOrEmpty(selectedFolderPath) || !filesToUpload.Any())
            {
                MessageBox.Show("Please select a folder and ensure there are files to sync.", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnUploadFolder.Enabled = false;
            btnUploadFile.Enabled = false;
            btnSyncFolder.Enabled = false;

            try
            {
                int days = 60;
                var objectTags = new List<Tag>();
                if (this.chkGrantUserRoleAccess.Checked)
                {
                    objectTags.Add(new Tag { Key = AppConstants.AppRoleTagKey, Value = AppConstants.RoleUser });
                }
                else
                {
                    objectTags.Add(new Tag { Key = AppConstants.AppRoleTagKey, Value = AppConstants.RoleAdmin });
                }

                // TODO: This call needs S3Service.SyncLocalFilesToS3Async to be updated to accept 'objectTags'
                // Similar to UploadFolderContentsAsync, this will be handled by modifying the S3Service method later.
                await _s3Service.SyncLocalFilesToS3Async(filesToUpload, selectedFolderPath, days, objectTags: objectTags);
                MessageBox.Show("Folder synchronization completed successfully!", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (AmazonS3Exception s3Ex)
            {
                MessageBox.Show($"Error during S3 operation: {s3Ex.Message}", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An unexpected error occurred: {ex.Message}", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnUploadFile.Enabled = true;
                btnSyncFolder.Enabled = true; // Re-enable based on your logic
            }
        }

        private async void btnUploadFile_Click(object sender, EventArgs e)
        {
            if (_s3Service == null) { MessageBox.Show("S3 Service not initialized.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string filePath = openFileDialog.FileName;
                string s3KeyName = Path.GetFileName(filePath); // Using filename as key by default for single upload

                lblSourceFileName.Text = filePath; // Update label

                if (MessageBox.Show($"Do you want to upload this file: {s3KeyName}?", this.Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    btnBrowseFolder.Enabled = false;
                    btnUploadFile.Enabled = false;
                    try
                    {
                        string fileExtension = Path.GetExtension(filePath).TrimStart('.');
                        string contentType = Misc.GetContentType(fileExtension); // Using the utility class

                        var objectTags = new List<Tag>();
                        if (this.chkGrantUserRoleAccess.Checked)
                        {
                            objectTags.Add(new Tag { Key = AppConstants.AppRoleTagKey, Value = AppConstants.RoleUser });
                        }
                        else
                        {
                            // Default for Admin upload if not granting to User
                            objectTags.Add(new Tag { Key = AppConstants.AppRoleTagKey, Value = AppConstants.RoleAdmin });
                        }

                        await _s3Service.UploadFileAsync(filePath, s3KeyName, contentType: contentType, acl: S3CannedACL.Private, objectTags: objectTags);
                        MessageBox.Show("File uploaded to S3 Bucket Successfully.", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (AmazonS3Exception s3Ex)
                    {
                        MessageBox.Show($"Error uploading file to S3: {s3Ex.Message}", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"An unexpected error occurred: {ex.Message}", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    finally
                    {
                        btnBrowseFolder.Enabled = true;
                        btnUploadFile.Enabled = true;
                    }
                }
            }
        }

        private async void btnUploadFolder_Click(object sender, EventArgs e)
        {
            if (_s3Service == null) { MessageBox.Show("S3 Service not initialized.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
            if (string.IsNullOrEmpty(selectedFolderPath) || !filesToUpload.Any())
            {
                MessageBox.Show("Please select a folder with files to upload.", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show("Do you want to upload all files in the selected folder to S3?", this.Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                btnSyncFolder.Enabled = false;
                btnUploadFolder.Enabled = false;
                try
                {
                    var objectTags = new List<Tag>();
                    if (this.chkGrantUserRoleAccess.Checked)
                    {
                        objectTags.Add(new Tag { Key = AppConstants.AppRoleTagKey, Value = AppConstants.RoleUser });
                    }
                    else
                    {
                        objectTags.Add(new Tag { Key = AppConstants.AppRoleTagKey, Value = AppConstants.RoleAdmin });
                    }

                    // TODO: This call needs S3Service.UploadFolderContentsAsync to be updated to accept 'objectTags'
                    // For now, this change highlights where tags would be passed.
                    // The actual modification of UploadFolderContentsAsync to accept and use tags will be a separate step for S3Service.cs
                    // For now, we will call it without the tags, and the next step will be to modify S3Service.UploadFolderContentsAsync
                    await _s3Service.UploadFolderContentsAsync(filesToUpload, selectedFolderPath, objectTags: objectTags); 
                    MessageBox.Show("Folder uploaded successfully!", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (AmazonS3Exception s3Ex)
                {
                    MessageBox.Show($"Error uploading folder to S3: {s3Ex.Message}", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An unexpected error occurred: {ex.Message}", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    btnSyncFolder.Enabled = true;
                    btnUploadFolder.Enabled = true;
                }
            }
        }

        private async void btnListS3Files_Click(object sender, EventArgs e)
        {
            if (_s3Service == null) { MessageBox.Show("S3 Service not initialized.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
            try
            {
                var excludeList = new List<string> { "logs/", "specificfile.logs" }; // Example exclude list
                var s3FileKeys = await _s3Service.ListBucketItemsAsync(excludeList);

                lstS3FilesBox.Items.Clear();
                if (s3FileKeys.Any())
                {
                    foreach (var key in s3FileKeys)
                    {
                        lstS3FilesBox.Items.Add(key);
                    }
                }
                else
                {
                    lstS3FilesBox.Items.Add("No files found in the bucket (matching criteria).");
                }
            }
            catch (AmazonS3Exception s3Ex)
            {
                MessageBox.Show($"Error listing S3 files: {s3Ex.Message}", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An unexpected error occurred: {ex.Message}", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void btnDownloadFiles_Click(object sender, EventArgs e)
        {
            if (_s3Service == null) { MessageBox.Show("S3 Service not initialized.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
            if (lstS3FilesBox.SelectedIndex == -1)
            {
                MessageBox.Show("Please select a file from the S3 list to download.", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string fileKey = lstS3FilesBox.SelectedItem.ToString();

            using (var saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.FileName = fileKey; // Or Path.GetFileName(fileKey)
                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string destinationPath = saveFileDialog.FileName;
                    try
                    {
                        using (Stream s3Stream = await _s3Service.DownloadFileAsStreamAsync(fileKey))
                        {
                            SaveStreamToFile(destinationPath, s3Stream); // Using existing local method
                        }
                        MessageBox.Show("File Downloaded from S3 Successfully.", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (AmazonS3Exception s3Ex)
                    {
                        MessageBox.Show($"Error downloading file from S3: {s3Ex.Message}", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"An unexpected error occurred: {ex.Message}", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private async void btnMoveToBackup_Click(object sender, EventArgs e)
        {
            if (_s3Service == null) { MessageBox.Show("S3 Service not initialized.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
            if (lstS3FilesBox.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select file(s) from the S3 list to move to backup.", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedKeys = lstS3FilesBox.SelectedItems.Cast<string>().ToList();
            if (MessageBox.Show($"Are you sure you want to move {selectedKeys.Count} file(s) to backup?", this.Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                try
                {
                    foreach (string fileKey in selectedKeys)
                    {
                        await _s3Service.BackupS3ObjectAsync(fileKey); // Uses the S3Service method for backup
                        Console.WriteLine($"Moved {fileKey} to backup folder and deleted the original.");
                    }
                    MessageBox.Show($"{selectedKeys.Count} file(s) moved to backup (typically a subfolder like 'backup-s3/').", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    // Refresh the list
                    btnListS3Files_Click(null, null);
                }
                catch (AmazonS3Exception s3Ex)
                {
                    MessageBox.Show($"Error moving files to backup: {s3Ex.Message}", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An unexpected error occurred: {ex.Message}", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // This method remains as it's a local utility
        private void SaveStreamToFile(string filePath, Stream inputStream)
        {
            using (FileStream outputStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                inputStream.CopyTo(outputStream);
            }
        }

        private void lstFiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool itemSelected = lstS3FilesBox.SelectedItem != null;
            bool itemsSelected = lstS3FilesBox.SelectedItems.Count > 0; // For multi-select actions

            btnDownloadFiles.Enabled = itemSelected; 
            // btnMoveToBackup was named btnDeleteFiles in Designer.cs, ensure consistency or update one.
            // Assuming btnDeleteFiles is the correct name for the 'Move to Backup' functionality control.
            if (this.Controls.ContainsKey("btnDeleteFiles")) // Check if btnDeleteFiles exists
                 (this.Controls["btnDeleteFiles"] as Button).Enabled = itemsSelected; 
            else if (this.Controls.ContainsKey("btnMoveToBackup")) // Fallback or alternative name
                 (this.Controls["btnMoveToBackup"] as Button).Enabled = itemsSelected;

            if (this.Controls.ContainsKey("btnManageRoles")) // Check if btnManageRoles exists
                 (this.Controls["btnManageRoles"] as Button).Enabled = itemsSelected;
        }

        private async void btnManageRoles_Click(object sender, EventArgs e)
        {
            if (_s3Service == null) { MessageBox.Show("S3 Service not initialized.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
            if (lstS3FilesBox.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select one or more S3 items to manage their roles.", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // For simplicity, this first version focuses on "Grant User Role Access"
            // A more complex UI could allow choosing which role or removing roles.
            DialogResult confirmation = MessageBox.Show("Grant 'User Role' access to the selected item(s)?\n\n(This will tag them as accessible by the 'User' role. Existing items not explicitly granted 'User' access are typically only accessible by Admin/Executive).", "Confirm Role Change", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (confirmation == DialogResult.Yes)
            {
                var userAccessTags = new List<Tag> { new Tag { Key = AppConstants.AppRoleTagKey, Value = AppConstants.RoleUser } };
                var itemsToProcess = lstS3FilesBox.SelectedItems.Cast<string>().ToList();
                int successCount = 0;
                int failCount = 0;

                this.Cursor = Cursors.WaitCursor; // Show wait cursor

                foreach (string itemKey in itemsToProcess)
                {
                    try
                    {
                        if (itemKey.EndsWith("/")) // Assume it's a folder/prefix
                        {
                            await _s3Service.ApplyTagsRecursivelyAsync(itemKey, userAccessTags);
                        }
                        else // Assume it's a file
                        {
                            await _s3Service.SetObjectTaggingAsync(itemKey, userAccessTags);
                        }
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        Console.WriteLine($"Failed to apply tags to {itemKey}: {ex.Message}");
                        // Optionally, collect detailed errors to show user later
                    }
                }
                this.Cursor = Cursors.Default; // Restore cursor

                string summaryMessage = $"{successCount} item(s) successfully tagged for 'User Role' access.";
                if (failCount > 0)
                {
                    summaryMessage += $"\n{failCount} item(s) failed. Check console/logs for details.";
                    MessageBox.Show(summaryMessage, "Role Management Partially Successful", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    MessageBox.Show(summaryMessage, "Role Management Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                // Refresh the S3 file list to reflect potential changes (though tags aren't directly visible here yet)
                btnListS3Files_Click(null, null);
            }
        }

        private void frmS3Access_Load(object sender, EventArgs e)
        {
            // Any load-time logic, e.g. initial population of S3 file list if desired
            // btnListS3Files_Click(null, null); // Example: Load S3 files on startup
        }
    }
}

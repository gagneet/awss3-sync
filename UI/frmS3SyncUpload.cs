using System;
using System.Collections.Generic; // Required for List<string>
using System.IO; // Required for Directory, Path
using System.Linq; // Required for .Any() and .Cast<string>()
using System.Windows.Forms;
using Amazon.S3.Model; // Added this line
// No Amazon S3 usings needed here directly if all S3 logic is in S3Service
// No Microsoft.Extensions.Configuration needed here
using AWSS3Sync.Core.S3; // Use the S3Service if direct calls are needed, though it's a partial class

namespace AWSS3Sync.UI // Updated namespace
{
    // This is a partial class, it will share the _s3Service instance from frmS3Sync.cs
    public partial class frmS3Sync : Form
    {
        // Event handler for btnSyncFolder_Click, which might be part of the frmS3Sync.Designer.cs
        // or defined in frmS3Sync.cs if it's the primary sync button.
        // The one from the original frmS3SyncUpload.cs is being adapted here.
        // If there are two buttons named btnSyncFolder, one needs to be renamed or its logic merged.
        // Assuming this btnSyncFolder_Click is the one from the original frmS3SyncUpload.cs context:

        private async void btnSyncFolder_UploadLogic_Click(object sender, EventArgs e) // Renamed to avoid conflict if another exists
        {
            // _s3Service is available here because it's a partial class with frmS3Sync
            if (_s3Service == null) { MessageBox.Show("S3 Service not initialized.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

            // Disable the File and Folder Upload buttons while synchronization is in progress
            // Assuming btnUploadFolder and btnUploadFile are accessible member controls
            if (Controls.ContainsKey("btnUploadFolder")) Controls["btnUploadFolder"].Enabled = false;
            if (Controls.ContainsKey("btnUploadFile")) Controls["btnUploadFile"].Enabled = false;

            try
            {
                if (lstLocalFilesBox.Items.Count == 0)
                {
                    MessageBox.Show("No files available in the listbox for synchronization.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // 'selectedFolderPath' should be set when files are listed in lstLocalFilesBox
                // 'filesToUpload' (List<string> of full paths) should also be populated.
                if (string.IsNullOrEmpty(selectedFolderPath) || !filesToUpload.Any())
                {
                     MessageBox.Show("Source folder not selected or no files listed for upload.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                
                // Ensure lblSourceFileName.Text contains the local folder path if that's the convention
                // String localFolderPathFromLabel = lblSourceFileName.Text; // This was used in original

                // The AdvancedSyncLocalToS3Async expects the base folder path and a list of full file paths.
                await _s3Service.AdvancedSyncLocalToS3Async(selectedFolderPath, filesToUpload);

                MessageBox.Show("File synchronization (advanced) completed successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (AmazonS3Exception s3Ex) // This using is missing: using Amazon.S3;
            {
                MessageBox.Show($"An error occurred during S3 synchronization: {s3Ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred during synchronization: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // Enable the File & Folder buttons after synchronization
                if (Controls.ContainsKey("btnUploadFolder")) Controls["btnUploadFolder"].Enabled = true;
                if (Controls.ContainsKey("btnUploadFile")) Controls["btnUploadFile"].Enabled = true;
            }
        }
        // The public async Task SyncFilesToS3Async method that was here is now removed,
        // its logic having been incorporated into S3Service.AdvancedSyncLocalToS3Async.
    }
}

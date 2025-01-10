using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AWSS3Sync
{
    public partial class frmS3Sync: Form
    {
        /*
         * Function to check the timestamp of the files on AWS S3 and then sync only new files
         */
        private async void btnSyncFolder_Click(object sender, EventArgs e)
        {
            // Disable the File and Folder Upload buttons while synchronization is in progress
            btnUploadFolder.Enabled = false;
            btnUploadFile.Enabled = false;

            try
            {
                if (lstLocalFilesBox.Items.Count == 0)
                {
                    MessageBox.Show("No files available in the listbox for synchronization.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                string localFolderPath = lblSourceFileName.Text; // Taking the folder path from the label
                string bucketName = _myBucketName; // You might want to get this from a TextBox as well

                await SyncFilesToS3Async(localFolderPath, bucketName);

                MessageBox.Show("File synchronization completed successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred during synchronization: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // Enable the File & Folder buttons after synchronization
                btnUploadFolder.Enabled = true;
                btnUploadFile.Enabled = true;
            }
        }


        public async Task SyncFilesToS3Async(string localFolderPath, string bucketName)
        {
            if (!Directory.Exists(localFolderPath))
            {
                Console.WriteLine($"Local folder '{localFolderPath}' does not exist.");
                return;
            }

            var s3ObjectsMetadata = await GetObjectsMetadataFromS3Async(bucketName);
            var localFilesSet = new HashSet<string>();

            foreach (var item in lstLocalFilesBox.Items)
            {
                string filePath = item.ToString(); // Assuming that the listbox contains the full paths of the files
                string fileName = Path.GetFileName(filePath);
                string s3Key = fileName;

                localFilesSet.Add(s3Key);

                if (!s3ObjectsMetadata.ContainsKey(s3Key))
                {
                    await UploadFileToS3Async(filePath, bucketName, s3Key);
                    Console.WriteLine($"Uploaded: {fileName}");
                }
                else
                {
                    DateTime localFileLastModified = File.GetLastWriteTimeUtc(filePath);
                    DateTime s3ObjectLastModified = s3ObjectsMetadata[s3Key];

                    if (localFileLastModified > s3ObjectLastModified)
                    {
                        await UploadFileToS3Async(filePath, bucketName, s3Key);
                        Console.WriteLine($"Uploaded: {fileName}");
                    }
                    else
                    {
                        Console.WriteLine($"File in S3 is up to date: {fileName}");
                    }
                }
            }

            // Move files not present locally but present in S3 to backup folder
            string backupBucketName = $"{bucketName}-backup";
            foreach (var s3Key in s3ObjectsMetadata.Keys)
            {
                if (!localFilesSet.Contains(s3Key))
                {
                    await MoveS3ObjectToBackup(bucketName, s3Key, backupBucketName);
                    Console.WriteLine($"Moved to backup: {s3Key}");
                }
            }
        }
    }
}

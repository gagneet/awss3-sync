using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AWSS3Sync
{
    public partial class frmS3Sync : Form
    {
        private readonly AmazonS3Client _s3Client;
        private readonly string _myBucketName;

        public frmS3Sync()
        {
            InitializeComponent();

            // Load configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            var awsOptions = configuration.GetSection("AWS");

            // Initialize your AWS S3 client
            _s3Client = new AmazonS3Client(
                awsOptions["AccessKey"],
                awsOptions["SecretKey"],
                Amazon.RegionEndpoint.GetBySystemName(awsOptions["Region"])
            );

            _myBucketName = awsOptions["BucketName"];
        }

        public virtual void InitializeForm()
        {
            // Implement any common initialization logic here
        }

        private string selectedFolderPath;
        private List<string> filesToUpload = new List<string>();

        /*
         * Function to select the folder which you will upload from the local system to S3
         */
        private void btnSelectFolder_Click(object sender, EventArgs e)
        {
            // Disable the Upload/Sync button
            btnSyncFolder.Enabled = false;
            btnUploadFile.Enabled = false;
            btnUploadFolder.Enabled = false;

            using (var folderBrowserDialog = new FolderBrowserDialog())
            {
                if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
                {
                    selectedFolderPath = folderBrowserDialog.SelectedPath;
                    filesToUpload = Directory.GetFiles(selectedFolderPath, "*", SearchOption.AllDirectories).ToList();

                    // Display files in the TextBox
                    lstLocalFilesBox.Items.Clear();
                    foreach (string filePath in filesToUpload)
                    {
                        //lstLocalFilesBox.AppendText(filePath + Environment.NewLine);
                        lstLocalFilesBox.Items.Add(filePath);
                    }
                }
            }

            // Enable the Upload & Sync buttons
            btnSyncFolder.Enabled = true;
            btnUploadFolder.Enabled = true;
            btnUploadFile.Enabled = true;
        }

        /*
         * Custom method to get the relative path, so that the same heirarchy can be created on S3
         */
        public static string GetRelativePath(string relativeTo, string path)
        {
            var fromUri = new Uri(relativeTo);
            var toUri = new Uri(path);

            if (fromUri.Scheme != toUri.Scheme)
            {
                return path; // path can't be made relative.
            }

            Uri relativeUri = fromUri.MakeRelativeUri(toUri);
            string relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            if (toUri.Scheme.Equals("file", StringComparison.InvariantCultureIgnoreCase))
            {
                relativePath = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }

            return relativePath;
        }

        /*
         * Function to check the timestamp of the files on AWS S3 and then sync only new files
         */
        private async void btnSyncFiles_Click(object sender, EventArgs e)
        {
            // disable the Sync & Upload button, so that user has to select the folder again
            btnUploadFolder.Enabled = false;
            btnUploadFile.Enabled = false;

            try
            {
                // Specify the number of days
                int days = 60; // Example: files changed in the last 60 days

                // Get the list of objects in the S3 bucket
                var existingS3Objects = await ListS3ObjectsAsync(_myBucketName);

                if (filesToUpload.Count == 0)
                {
                    MessageBox.Show("No files to upload from the local folder.", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                foreach (string filePath in filesToUpload)
                {
                    string fileName = Path.GetFileName(filePath);
                    // Use the below for more precise calculation on newer .NET frameworks/core
                    // string relativePath = GetRelativePath(selectedFolderPath, filePath).Replace("\\", "/", StringComparison.Ordinal); // Replace backslashes with forward slashes for S3 compatibility
                    string relativePath = GetRelativePath(selectedFolderPath, filePath).Replace("\\", "/"); // Replace backslashes with forward slashes for S3 compatibility
                    DateTime localFileLastModified = File.GetLastWriteTimeUtc(filePath);

                    bool shouldUpload = false;

                    // Check if the file exists in S3 and compare last modified dates
                    if (existingS3Objects.TryGetValue(relativePath, out var s3ObjectMetadata))
                    {
                        if (localFileLastModified > s3ObjectMetadata.LastModified)
                        {
                            shouldUpload = true;
                        }
                    }
                    else
                    {
                        // File does not exist in S3, mark for upload
                        shouldUpload = true;
                    }

                    // Check if the file was modified in the last X days
                    if (localFileLastModified > DateTime.UtcNow.AddDays(-days))
                    {
                        shouldUpload = true;
                    }

                    // Upload the file if it is new or has been updated
                    if (shouldUpload)
                    {
                        using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                        {
                            var putObjectRequest = new PutObjectRequest
                            {
                                BucketName = _myBucketName,
                                Key = relativePath,
                                InputStream = fileStream,
                                AutoCloseStream = true
                            };

                            await _s3Client.PutObjectAsync(putObjectRequest);
                            Console.WriteLine($"Uploaded: {relativePath}");
                        }
                    }
                }

                MessageBox.Show("Folder synchronization completed successfully!", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (AmazonS3Exception s3Ex)
            {
                MessageBox.Show($"Error uploading file to S3: {s3Ex.Message}", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An unexpected error occurred: {ex.Message}", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            // Enable the Upload file and folder buttons, so that user has to select the folder again
            btnUploadFolder.Enabled = false;
            btnUploadFile.Enabled = true;
        }

        private async Task<Dictionary<string, S3Object>> ListS3ObjectsAsync(string bucketName)
        {
            var s3Objects = new Dictionary<string, S3Object>();
            string continuationToken = null;

            do
            {
                var request = new ListObjectsV2Request
                {
                    BucketName = bucketName,
                    ContinuationToken = continuationToken
                };

                var response = await _s3Client.ListObjectsV2Async(request);

                foreach (var s3Object in response.S3Objects)
                {
                    s3Objects[s3Object.Key] = s3Object;
                }

                continuationToken = response.NextContinuationToken;
            } while (continuationToken != null);

            return s3Objects;
        }

        private async void btnUploadFile_Click(object sender, EventArgs e)
        {
            // Disable the browse/select folder button, so that user does not accidently select a new folder
            btnBrowseFolder.Enabled = false;

            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string filePath = openFileDialog.FileName;
                string fileName = Path.GetFileName(filePath);

                lblSourceFileName.Text = openFileDialog.FileName;

                if (MessageBox.Show("Do you want to upload this file?", this.Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    string fileExtension = filePath.Substring(filePath.LastIndexOf(".") + 1);
                    string contentType = Code.Misc.GetContentType(fileExtension);

                    try
                    {
                        using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                        {
                            var putObjectRequest = new PutObjectRequest
                            {
                                BucketName = _myBucketName,
                                Key = fileName,
                                InputStream = fileStream,
                                ContentType = contentType,
                                CannedACL = S3CannedACL.Private,
                                AutoCloseStream = true
                            };

                            PutObjectResponse response = await _s3Client.PutObjectAsync(putObjectRequest);

                            if (response.ETag != null)
                            {
                                string etag = response.ETag;
                                string versionID = response.VersionId;

                                MessageBox.Show("File uploaded to S3 Bucket Successfully.", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }
                    }
                    catch (AmazonS3Exception s3Ex)
                    {
                        MessageBox.Show($"Error uploading file to S3: {s3Ex.Message}", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"An unexpected error occurred: {ex.Message}", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }

            // Enable the Browse/Select Folder button
            btnBrowseFolder.Enabled = true;
        }

        /*
         * Same method as above, but has a fail-safe to ensure that the file exists, before using the Key/ID
         * 
        private async void btnUploadFile_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string filePath = openFileDialog.FileName;
                string fileName = Path.GetFileName(filePath);

                lblSourceFileName.Text = openFileDialog.FileName;

                if (MessageBox.Show("Do you want to upload this file?", this.Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    string fileExtension = filePath.Substring(filePath.LastIndexOf(".") + 1);
                    string contentType = Code.Misc.GetContentType(fileExtension);

                    try
                    {
                        // Check if the object already exists
                        try
                        {
                            var metadataRequest = new GetObjectMetadataRequest
                            {
                                BucketName = _myBucketName,
                                Key = fileName
                            };

                            var metadataResponse = await _s3Client.GetObjectMetadataAsync(metadataRequest);

                            // If we get here, the object exists
                            MessageBox.Show($"File '{fileName}' already exists in the S3 bucket. Upload cancelled.", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            // Object does not exist, we can proceed with the upload
                        }

                        using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                        {
                            var putObjectRequest = new PutObjectRequest
                            {
                                BucketName = _myBucketName,
                                Key = fileName,
                                InputStream = fileStream,
                                ContentType = contentType,
                                CannedACL = S3CannedACL.Private,
                                AutoCloseStream = true
                            };

                            PutObjectResponse response = await _s3Client.PutObjectAsync(putObjectRequest);

                            if (response.ETag != null)
                            {
                                string etag = response.ETag;
                                string versionID = response.VersionId;

                                MessageBox.Show("File uploaded to S3 Bucket Successfully.", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }
                    }
                    catch (AmazonS3Exception s3Ex)
                    {
                        MessageBox.Show($"Error uploading file to S3: {s3Ex.Message}", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"An unexpected error occurred: {ex.Message}", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
        */


        private async void btnUploadFolder_Click(object sender, EventArgs e)
        {
            // Disable the Sync & Upload button, so that user has to select the folder again
            btnSyncFolder.Enabled = false;

            if (MessageBox.Show("Do you want to upload the listed files to S3?", this.Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                try
                {
                    foreach (string filePath in filesToUpload)
                    {
                        string relativePath = GetRelativePath(selectedFolderPath, filePath).Replace("\\", "/"); // Replace backslashes with forward slashes for S3 compatibility

                        using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                        {
                            var putObjectRequest = new PutObjectRequest
                            {
                                BucketName = _myBucketName,
                                Key = relativePath,
                                InputStream = fileStream,
                                AutoCloseStream = true
                            };

                            await _s3Client.PutObjectAsync(putObjectRequest);
                            Console.WriteLine($"Uploaded: {relativePath}");
                        }
                    }

                    MessageBox.Show("Folder uploaded successfully!", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (AmazonS3Exception s3Ex)
                {
                    MessageBox.Show($"Error uploading file to S3: {s3Ex.Message}", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An unexpected error occurred: {ex.Message}", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            // Enable the Sync & Upload button
            btnSyncFolder.Enabled = true;
        }

        /*
        * Previous implementation of the Folder Upload
        * 
        private async void btnUploadFolder_Click(object sender, EventArgs e)
        {
            using (var folderBrowserDialog = new FolderBrowserDialog())
            {
                if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
                {
                    string folderPath = folderBrowserDialog.SelectedPath;

                    lblSourceFileName.Text = folderBrowserDialog.SelectedPath;

                    foreach (string filePath in Directory.GetFiles(folderPath))
                    {
                        try
                        {
                            string fileName = Path.GetFileName(filePath);
                            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                            {
                                var putObjectRequest = new PutObjectRequest
                                {
                                    BucketName = _myBucketName,
                                    Key = fileName,
                                    InputStream = fileStream,
                                    AutoCloseStream = true
                                };

                                await _s3Client.PutObjectAsync(putObjectRequest);
                            }

                            Console.WriteLine($"Uploaded: {fileName}");
                        }
                        catch (Exception ex)
                        {
                            string fileName = Path.GetFileName(filePath);
                            Console.WriteLine($"Error uploading file: {fileName} - {ex.Message}");
                        }
                    }

                    MessageBox.Show("Folder uploaded successfully!");
                }
            }
        }
        */


        private async Task MoveS3ObjectToBackup(string sourceBucketName, string sourceKey, string backupBucketName)
        {
            var copyRequest = new CopyObjectRequest
            {
                SourceBucket = sourceBucketName,
                SourceKey = sourceKey,
                DestinationBucket = backupBucketName,
                DestinationKey = sourceKey
            };

            await _s3Client.CopyObjectAsync(copyRequest);
            await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = sourceBucketName,
                Key = sourceKey
            });
        }

        private async Task<Dictionary<string, DateTime>> GetObjectsMetadataFromS3Async(string bucketName)
        {
            var s3ObjectsMetadata = new Dictionary<string, DateTime>();

            var request = new ListObjectsRequest
            {
                BucketName = bucketName
            };

            ListObjectsResponse response;
            do
            {
                response = await _s3Client.ListObjectsAsync(request); // Use ListObjectsAsync

                foreach (var s3Object in response.S3Objects)
                {
                    var metadataRequest = new GetObjectMetadataRequest
                    {
                        BucketName = bucketName,
                        Key = s3Object.Key
                    };

                    var metadataResponse = await _s3Client.GetObjectMetadataAsync(metadataRequest);
                    s3ObjectsMetadata[s3Object.Key] = metadataResponse.LastModified.ToUniversalTime();
                }

                request.Marker = response.NextMarker; // Use NextMarker for pagination
            } while (response.IsTruncated);

            return s3ObjectsMetadata;
        }

        private async Task UploadFileToS3Async(string filePath, string bucketName, string s3Key)
        {
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                var putObjectRequest = new PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = s3Key,
                    InputStream = fileStream,
                    AutoCloseStream = true
                };

                await _s3Client.PutObjectAsync(putObjectRequest);
            }
        }

        /*
         * Method to list all the files and folders which are present on a specified S3 bucket
         * Added additional logic, to exclude certain folders from being shown in the list, like logs/
         */
        private async void btnListS3Files_Click(object sender, EventArgs e)
        {
            try
            {
                // List of folders or files to exclude
                var excludeList = new List<string>
                {
                    "logs/",
                    "specificfile.logs"
                };

                var listObjectsV2Request = new ListObjectsV2Request
                {
                    BucketName = _myBucketName
                };

                var response = await _s3Client.ListObjectsV2Async(listObjectsV2Request);

                if (response.S3Objects != null && response.S3Objects.Count > 0)
                {
                    lstS3FilesBox.Items.Clear();
                    foreach (var s3Object in response.S3Objects)
                    {
                        // Check if the object key starts with any of the excluded folders
                        bool isExcluded = excludeList.Any(excludedPath => s3Object.Key.StartsWith(excludedPath, StringComparison.OrdinalIgnoreCase));

                        if (!isExcluded)
                        {
                            lstS3FilesBox.Items.Add(s3Object.Key);
                        }
                    }
                }
                else
                {
                    lstS3FilesBox.Items.Clear();
                    lstS3FilesBox.Items.Add("No files found in the bucket: " + _myBucketName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error listing files: {ex.Message}");
            }
        }

        private void btnDownloadFiles_Click(object sender, EventArgs e)
        {
            if (lstS3FilesBox.SelectedIndex != -1)
            {
                using (var saveFileDialog = new SaveFileDialog())
                {
                    saveFileDialog.FileName = lstS3FilesBox.SelectedItem.ToString();
                    if (saveFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        try
                        {
                            string fileKey = lstS3FilesBox.SelectedItem.ToString();
                            string filePath = saveFileDialog.FileName;

                            using (Stream fileStream = new MemoryStream())
                            {
                                GetObjectRequest request = new GetObjectRequest
                                {
                                    BucketName = _myBucketName,
                                    Key = fileKey,
                                };
                                using (GetObjectResponse response = _s3Client.GetObject(request))
                                {
                                    response.ResponseStream.CopyTo(fileStream);
                                }

                                fileStream.Position = 0;

                                SaveStreamToFile(filePath, fileStream);

                                MessageBox.Show("File Downloaded from S3 Successfully.", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error downloading file: {ex.Message}", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            }
        }

        private async void btnMoveToBackup_Click(object sender, EventArgs e)
        {
            try
            {
                foreach (var selectedItem in lstS3FilesBox.SelectedItems)
                {
                    string fileKey = selectedItem.ToString();
                    string backupKey = $"backup-s3/{fileKey}";

                    // Copy the file to the backup folder
                    var copyRequest = new CopyObjectRequest
                    {
                        SourceBucket = _myBucketName,
                        SourceKey = fileKey,
                        DestinationBucket = _myBucketName,
                        DestinationKey = backupKey
                    };

                    await _s3Client.CopyObjectAsync(copyRequest);

                    // Delete the original file
                    var deleteRequest = new DeleteObjectRequest
                    {
                        BucketName = _myBucketName,
                        Key = fileKey
                    };

                    await _s3Client.DeleteObjectAsync(deleteRequest);

                    Console.WriteLine($"Moved {fileKey} to {backupKey} and deleted the original.");
                }

                MessageBox.Show("Selected files moved to backup and will be deleted in 28 days.", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
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

        private void SaveStreamToFile(string filePath, Stream inputStream)
        {
            using (FileStream outputStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                inputStream.CopyTo(outputStream);
            }
        }

        private void lstFiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            btnDownloadFiles.Enabled = lstS3FilesBox.Items.Count >= 1;
        }

        private void frmS3Access_Load(object sender, EventArgs e)
        {

        }
    }
}

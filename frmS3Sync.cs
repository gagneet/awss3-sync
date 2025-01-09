using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Drawing;
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

        private string selectedFolderPath;
        private List<string> filesToUpload = new List<string>();

        private void btnSelectFolder_Click(object sender, EventArgs e)
        {
            // Disable the Upload/Sync button
            btnSyncFolder.Enabled = true;

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

                    // Enable the Upload button
                    btnSyncFolder.Enabled = true;
                }
            }
        }

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

        private async void btnSyncFiles_Click(object sender, EventArgs e)
        {
            try
            {
                // Get the list of objects in the S3 bucket
                var existingS3Objects = await ListS3ObjectsAsync(_myBucketName);

                foreach (string filePath in filesToUpload)
                {
                    string fileName = Path.GetFileName(filePath);
                    string relativePath = GetRelativePath(selectedFolderPath, filePath).Replace("\\", "/"); // Replace backslashes with forward slashes for S3 compatibility

                    // Check if the file exists in S3 and compare last modified dates
                    if (existingS3Objects.TryGetValue(relativePath, out var s3ObjectMetadata))
                    {
                        var localFileLastModified = File.GetLastWriteTimeUtc(filePath);

                        if (localFileLastModified <= s3ObjectMetadata.LastModified)
                        {
                            // The local file is not newer, skip uploading
                            continue;
                        }
                    }

                    // The file is new or has been updated, upload it
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

                MessageBox.Show("Folder synchronization completed successfully!");
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
        }

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

        private async void btnSyncFolder_Click(object sender, EventArgs e)
        {
            using (var folderBrowserDialog = new FolderBrowserDialog())
            {
                if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
                {
                    string localFolderPath = folderBrowserDialog.SelectedPath;
                    string bucketName = _myBucketName; // You might want to get this from a TextBox as well

                    try
                    {
                        await SyncFilesToS3Async(localFolderPath, bucketName);

                        MessageBox.Show("File synchronization completed successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"An error occurred during synchronization: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        public async Task SyncFilesToS3Async(string localFolderPath, string bucketName)
        {
            if (!Directory.Exists(localFolderPath))
            {
                Console.WriteLine($"Local folder '{localFolderPath}' does not exist.");
                return;
            }

            var s3Objects = await GetObjectsFromS3Async(bucketName);

            foreach (var filePath in Directory.GetFiles(localFolderPath))
            {
                string fileName = Path.GetFileName(filePath);
                string s3Key = fileName;

                if (!s3Objects.Contains(s3Key))
                {
                    await UploadFileToS3Async(filePath, bucketName, s3Key);
                    Console.WriteLine($"Uploaded: {fileName}");
                }
                else
                {
                    Console.WriteLine($"File already exists in S3: {fileName}");
                }
            }
        }

        private async Task<HashSet<string>> GetObjectsFromS3Async(string bucketName)
        {
            var s3Objects = new HashSet<string>();

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
                    s3Objects.Add(s3Object.Key);
                }

                request.Marker = response.NextMarker; // Use NextMarker for pagination
            } while (response.IsTruncated);

            return s3Objects;
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

        private async void btnListS3Files_Click(object sender, EventArgs e)
        {
            try
            {
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
                        lstS3FilesBox.Items.Add(s3Object.Key);
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

        /*
         * Alternate List Files, with the older AWS control
         * 
        private void btnListS3Files_Click(object sender, EventArgs e)
        {
            lstS3FilesBox.Items.Clear();
            try
            {

                ListObjectsV2Request request = new ListObjectsV2Request();
                request.BucketName = _myBucketName;
                do
                {
                    ListObjectsV2Response response = _s3Client.ListObjects(request);

                    // Process response.
                    // ...
                    response.S3Objects.ForEach(x => { lstS3FilesBox.Items.Add(x.Key); });
                    // If response is truncated, set the marker to get the next 
                    // set of keys.
                    if (response.IsTruncated)
                    {
                        request.Marker = response.NextMarker;
                    }
                    else
                    {
                        request = null;
                    }
                } while (request != null);
            }
            catch (Exception ex)
            {

                MessageBox.Show(ex.Message, this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        */

        private void btnDownloadFiles_Click(object sender, EventArgs e)
        {
            if (lstS3FilesBox.SelectedIndex != -1)
            {
                using (var savedi = new SaveFileDialog())
                {
                    savedi.FileName = lstS3FilesBox.SelectedItem.ToString();
                    if (savedi.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        try
                        {
                            string imageKey = lstS3FilesBox.SelectedItem.ToString();
                            string extension = imageKey.Substring(imageKey.LastIndexOf("."));
                            string imagePath = savedi.FileName;

                            Stream imageStream = new MemoryStream();


                            GetObjectRequest request = new GetObjectRequest
                            {
                                BucketName = _myBucketName,
                                Key = imageKey,
                            };
                            using (GetObjectResponse response = _s3Client.GetObject(request))
                            {
                                response.ResponseStream.CopyTo(imageStream);
                            }

                            imageStream.Position = 0;

                            Code.Misc.SaveStreamToFile(imagePath, imageStream);

                            if (chkImagePReview.Checked)
                                picPreview.Image = Image.FromFile(imagePath);

                            MessageBox.Show("File Downloaded from S3 Successfully.", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message, this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
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

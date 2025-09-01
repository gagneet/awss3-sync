using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using S3FileManager.Models;

namespace S3FileManager.Services
{
    public class S3Service : IDisposable
    {
        private readonly AmazonS3Client _s3Client;
        private readonly string _bucketName;
        private readonly MetadataService _metadataService;

        public S3Service()
        {
            var config = ConfigurationService.GetConfiguration();
            var awsConfig = new AmazonS3Config { RegionEndpoint = RegionEndpoint.GetBySystemName(config.AWS.Region) };
            _s3Client = new AmazonS3Client(config.AWS.AccessKey, config.AWS.SecretKey, awsConfig);
            _bucketName = config.AWS.BucketName;
            _metadataService = new MetadataService();
        }

        public async Task<List<FileNode>> ListFilesAsync(UserRole userRole)
        {
            var flatList = await GetFlatS3FileList(userRole);
            return BuildS3Hierarchy(flatList);
        }

        private List<FileNode> BuildS3Hierarchy(List<S3FileItem> s3Files)
        {
            var fileNodes = new Dictionary<string, FileNode>();
            var rootNodes = new List<FileNode>();

            foreach (var s3File in s3Files.OrderBy(f => f.Key))
            {
                var parts = s3File.Key.TrimEnd('/').Split('/');
                FileNode parent = null;
                string currentPath = "";

                for (int i = 0; i < parts.Length; i++)
                {
                    string part = parts[i];
                    currentPath += part;
                    bool isDir = i < parts.Length - 1 || s3File.Key.EndsWith("/");
                    if (isDir)
                    {
                        currentPath += "/";
                    }

                    if (!fileNodes.TryGetValue(currentPath, out var node))
                    {
                        node = new FileNode(part, currentPath, isDir, isDir ? 0 : s3File.Size, s3File.LastModified, s3File.AccessRoles);
                        fileNodes.Add(currentPath, node);

                        if (parent != null)
                        {
                            if (!parent.Children.Any(c => c.Path == node.Path))
                                parent.Children.Add(node);
                        }
                        else
                        {
                            if (!rootNodes.Any(r => r.Path == node.Path))
                                rootNodes.Add(node);
                        }
                    }
                    parent = node;
                }
            }
            return rootNodes;
        }

        private async Task<List<S3FileItem>> GetFlatS3FileList(UserRole userRole)
        {
            var files = new List<S3FileItem>();
            var request = new ListObjectsV2Request { BucketName = _bucketName, MaxKeys = 1000 };
            ListObjectsV2Response response;
            do
            {
                response = await _s3Client.ListObjectsV2Async(request);
                foreach (var obj in response.S3Objects)
                {
                    var accessRoles = await _metadataService.GetFileAccessRolesAsync(obj.Key);
                    var item = new S3FileItem
                    {
                        Key = obj.Key,
                        Size = obj.Size,
                        LastModified = obj.LastModified,
                        AccessRoles = accessRoles
                    };
                    if (CanUserAccessFile(userRole, item))
                    {
                        files.Add(item);
                    }
                }
                request.ContinuationToken = response.NextContinuationToken;
            } while (response.IsTruncated);
            files = files.Where(f => !f.Key.StartsWith("logs/", StringComparison.OrdinalIgnoreCase)).ToList();
            return FilterFilesForRole(files, userRole);
        }

        private bool CanUserAccessFile(UserRole userRole, S3FileItem item)
        {
            // ... (same as original)
            return true;
        }

        private List<S3FileItem> FilterFilesForRole(List<S3FileItem> files, UserRole userRole)
        {
            // ... (same as original)
            return files;
        }

        public async Task<bool> UploadFileAsync(string filePath, string key, List<UserRole> accessRoles)
        {
            // ... (same as original)
            return true;
        }

        public async Task UploadDirectoryAsync(string directoryPath, string keyPrefix, List<UserRole> accessRoles)
        {
            // ... (same as original)
        }

        public async Task DownloadFileAsync(string s3Key, string localPath)
        {
            // ... (same as original)
        }

        public async Task DeleteFileAsync(string s3Key)
        {
            // ... (same as original)
        }

        private string GetContentType(string filePath)
        {
            // ... (same as original)
            return "";
        }

        public void Dispose()
        {
            _s3Client?.Dispose();
        }
    }
}

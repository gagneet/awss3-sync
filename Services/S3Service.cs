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
                        Size = obj.Size ?? 0,
                        LastModified = obj.LastModified ?? DateTime.MinValue,
                        AccessRoles = accessRoles
                    };
                    if (CanUserAccessFile(userRole, item))
                    {
                        files.Add(item);
                    }
                }
                request.ContinuationToken = response.NextContinuationToken;
            } while (response.IsTruncated.GetValueOrDefault());
            files = files.Where(f => !f.Key.StartsWith("logs/", StringComparison.OrdinalIgnoreCase)).ToList();
            return FilterFilesForRole(files, userRole);
        }

        private bool CanUserAccessFile(UserRole userRole, S3FileItem item)
        {
            switch (userRole)
            {
                case UserRole.Administrator:
                    return true;
                case UserRole.Executive:
                    return item.AccessRoles.Contains(UserRole.Executive) ||
                           item.AccessRoles.Contains(UserRole.Administrator) ||
                           item.Key.StartsWith("executive-committee/");
                case UserRole.User:
                    return item.AccessRoles.Contains(UserRole.User);
                default:
                    return false;
            }
        }

        private List<S3FileItem> FilterFilesForRole(List<S3FileItem> files, UserRole userRole)
        {
            if (userRole == UserRole.Administrator)
                return files;

            var filteredFiles = new List<S3FileItem>();
            var accessiblePaths = new HashSet<string>();

            foreach (var file in files)
            {
                if (CanUserAccessFile(userRole, file))
                {
                    filteredFiles.Add(file);
                    var pathParts = file.Key.Split('/');
                    var currentPath = "";
                    for (int i = 0; i < pathParts.Length - 1; i++)
                    {
                        currentPath += pathParts[i] + "/";
                        accessiblePaths.Add(currentPath);
                    }
                }
            }

            foreach (var path in accessiblePaths)
            {
                if (!filteredFiles.Any(f => f.Key == path))
                {
                    filteredFiles.Add(new S3FileItem
                    {
                        Key = path,
                        Size = 0,
                        LastModified = DateTime.Now,
                        AccessRoles = new List<UserRole> { userRole }
                    });
                }
            }

            return filteredFiles.OrderBy(f => f.Key).ToList();
        }

        private class S3ObjectAttributes
        {
            public DateTime LastModified { get; set; }
            public long Size { get; set; }
        }

        private async Task<S3ObjectAttributes> GetS3ObjectAttributesAsync(string key)
        {
            try
            {
                var request = new GetObjectMetadataRequest { BucketName = _bucketName, Key = key };
                var metadata = await _s3Client.GetObjectMetadataAsync(request);
                return new S3ObjectAttributes
                {
                    LastModified = metadata.LastModified.ToUniversalTime(),
                    Size = metadata.ContentLength
                };
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task<bool> UploadFileAsync(string filePath, string key, List<UserRole> accessRoles)
        {
            var localFileInfo = new FileInfo(filePath);
            var localFileLastWriteTimeUtc = localFileInfo.LastWriteTimeUtc;
            var localFileSize = localFileInfo.Length;

            var s3Attributes = await GetS3ObjectAttributesAsync(key);
            bool performUpload = true;

            if (s3Attributes != null)
            {
                if (localFileLastWriteTimeUtc <= s3Attributes.LastModified && localFileSize == s3Attributes.Size)
                {
                    performUpload = false;
                }
            }

            if (performUpload)
            {
                var request = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = key,
                    FilePath = filePath,
                    ContentType = GetContentType(filePath)
                };
                await _s3Client.PutObjectAsync(request);
                await _metadataService.SetFileAccessRolesAsync(key, accessRoles);
                return true;
            }
            return false;
        }

        public async Task UploadDirectoryAsync(string directoryPath, string keyPrefix, List<UserRole> accessRoles)
        {
            string cleanKeyPrefix = keyPrefix.Trim('/');

            try
            {
                // Upload files in the current directory
                foreach (string file in Directory.GetFiles(directoryPath))
                {
                    string fileName = Path.GetFileName(file);
                    string key = string.IsNullOrEmpty(cleanKeyPrefix) ? fileName : $"{cleanKeyPrefix}/{fileName}";
                    await UploadFileAsync(file, key, accessRoles);
                }

                // Recursively upload subdirectories
                foreach (string subDir in Directory.GetDirectories(directoryPath))
                {
                    string dirName = Path.GetFileName(subDir);
                    string newKeyPrefix = string.IsNullOrEmpty(cleanKeyPrefix) ? dirName : $"{cleanKeyPrefix}/{dirName}";
                    await UploadDirectoryAsync(subDir, newKeyPrefix, accessRoles);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories that we don't have permission to access
            }
        }

        public async Task<string> DownloadFileAsync(string s3Key, string localDirectory)
        {
            string fileName = Path.GetFileName(s3Key);
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = Guid.NewGuid().ToString(); // Create a unique name if one can't be determined
            }
            string fullPath = Path.Combine(localDirectory, fileName);
            Directory.CreateDirectory(localDirectory);

            var request = new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = s3Key
            };

            using (var response = await _s3Client.GetObjectAsync(request))
            using (var responseStream = response.ResponseStream)
            using (var fileStream = File.Create(fullPath))
            {
                await responseStream.CopyToAsync(fileStream);
            }
            return fullPath;
        }

        public async Task DeleteFileAsync(string s3Key)
        {
            var request = new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = s3Key
            };
            await _s3Client.DeleteObjectAsync(request);
            await _metadataService.RemoveFileAccessRolesAsync(s3Key);
        }

        private string GetContentType(string filePath)
        {
            return Path.GetExtension(filePath).ToLowerInvariant() switch
            {
                ".txt" => "text/plain",
                ".html" or ".htm" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".json" => "application/json",
                ".xml" => "application/xml",
                ".pdf" => "application/pdf",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".svg" => "image/svg+xml",
                ".zip" => "application/zip",
                _ => "application/octet-stream",
            };
        }

        public void Dispose()
        {
            _s3Client?.Dispose();
        }
    }
}

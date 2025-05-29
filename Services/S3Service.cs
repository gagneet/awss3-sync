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

            var awsConfig = new AmazonS3Config
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(config.AWS.Region)
            };

            _s3Client = new AmazonS3Client(config.AWS.AccessKey, config.AWS.SecretKey, awsConfig);
            _bucketName = config.AWS.BucketName;
            _metadataService = new MetadataService();
        }

        public async Task<List<S3FileItem>> ListFilesAsync(UserRole userRole)
        {
            var files = new List<S3FileItem>();

            var request = new ListObjectsV2Request
            {
                BucketName = _bucketName,
                MaxKeys = 1000
            };

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
                        LastModified = obj.LastModified ?? DateTime.Now,
                        AccessRoles = accessRoles
                    };

                    // Apply role-based filtering
                    if (CanUserAccessFile(userRole, item))
                    {
                        files.Add(item);
                    }
                }

                request.ContinuationToken = response.NextContinuationToken;
            } while (response.IsTruncated ?? false);

            // Globally filter out any S3 objects under a root "logs/" folder
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

            // First pass: collect all accessible files and their paths
            foreach (var file in files)
            {
                if (CanUserAccessFile(userRole, file))
                {
                    filteredFiles.Add(file);

                    // Add all parent directories to show folder structure
                    var pathParts = file.Key.Split('/');
                    var currentPath = "";
                    for (int i = 0; i < pathParts.Length - 1; i++)
                    {
                        currentPath += pathParts[i] + "/";
                        accessiblePaths.Add(currentPath);
                    }
                }
            }

            // Second pass: add necessary folder structure
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

        private async Task<S3ObjectAttributes?> GetS3ObjectAttributesAsync(string key)
        {
            try
            {
                var request = new GetObjectMetadataRequest
                {
                    BucketName = _bucketName,
                    Key = key
                };
                var metadata = await _s3Client.GetObjectMetadataAsync(request);
                return new S3ObjectAttributes
                {
                    LastModified = metadata.LastModified.ToUniversalTime(), // Ensure UTC
                    Size = metadata.ContentLength
                };
            }
            catch (Amazon.S3.AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null; // Object does not exist
            }
            // Allow other exceptions to propagate
        }

        public async Task<bool> UploadFileAsync(string filePath, string key, List<UserRole> accessRoles)
        {
            var localFileInfo = new FileInfo(filePath);
            var localFileLastWriteTimeUtc = localFileInfo.LastWriteTimeUtc;
            var localFileSize = localFileInfo.Length;

            var s3ObjectAttributes = await GetS3ObjectAttributesAsync(key);

            bool shouldUpload = false;
            if (s3ObjectAttributes == null)
            {
                shouldUpload = true; // S3 object does not exist
            }
            else
            {
                // Compare LastModified (S3 is UTC, ensure local is UTC)
                if (localFileLastWriteTimeUtc > s3ObjectAttributes.LastModified)
                {
                    shouldUpload = true; // Local file is newer
                }
                else if (localFileLastWriteTimeUtc == s3ObjectAttributes.LastModified)
                {
                    if (localFileSize != s3ObjectAttributes.Size)
                    {
                        shouldUpload = true; // Timestamps are identical, but sizes differ
                    }
                }
                // else: S3 object is newer or same timestamp and size, so no upload
            }

            if (shouldUpload)
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
                return true; // Uploaded
            }
            return false; // Skipped
        }

        public async Task UploadDirectoryAsync(string directoryPath, string keyPrefix, List<UserRole> accessRoles)
        {
            // Ensure keyPrefix is correctly formatted (no leading/trailing slashes for internal logic)
            string cleanKeyPrefix = keyPrefix.Trim('/');

            foreach (string file in Directory.GetFiles(directoryPath))
            {
                string fileName = Path.GetFileName(file);
                // Construct file key: if cleanKeyPrefix is empty, key is just fileName, otherwise prefix/fileName
                string key = string.IsNullOrEmpty(cleanKeyPrefix) ? fileName : $"{cleanKeyPrefix}/{fileName}";
                await UploadFileAsync(file, key, accessRoles); // UploadFileAsync now returns bool, can be used later
            }

            foreach (string subDir in Directory.GetDirectories(directoryPath))
            {
                string dirName = Path.GetFileName(subDir);
                // Construct newKeyPrefix for subdirectory: if cleanKeyPrefix is empty, new prefix is just dirName, otherwise prefix/dirName
                string newKeyPrefixForSubDir = string.IsNullOrEmpty(cleanKeyPrefix) ? dirName : $"{cleanKeyPrefix}/{dirName}";
                await UploadDirectoryAsync(subDir, newKeyPrefixForSubDir, accessRoles);
            }
        }

        public async Task DownloadFileAsync(string s3Key, string localPath)
        {
            string fileName = Path.GetFileName(s3Key);
            if (string.IsNullOrEmpty(fileName))
                fileName = s3Key.Replace('/', '_');

            string fullPath = Path.Combine(localPath, fileName);

            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

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
            string extension = Path.GetExtension(filePath).ToLower();
            return extension switch
            {
                ".txt" => "text/plain",
                ".html" or ".htm" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".json" => "application/json",
                ".pdf" => "application/pdf",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".zip" => "application/zip",
                _ => "application/octet-stream"
            };
        }

        public void Dispose()
        {
            _s3Client?.Dispose();
        }
    }
}
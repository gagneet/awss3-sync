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

        public async Task UploadFileAsync(string filePath, string key, List<UserRole> accessRoles)
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
        }

        public async Task UploadDirectoryAsync(string directoryPath, string keyPrefix, List<UserRole> accessRoles)
        {
            foreach (string file in Directory.GetFiles(directoryPath))
            {
                string fileName = Path.GetFileName(file);
                string key = $"{keyPrefix}/{fileName}";
                await UploadFileAsync(file, key, accessRoles);
            }

            foreach (string subDir in Directory.GetDirectories(directoryPath))
            {
                string dirName = Path.GetFileName(subDir);
                string newKeyPrefix = $"{keyPrefix}/{dirName}";
                await UploadDirectoryAsync(subDir, newKeyPrefix, accessRoles);
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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using AWSS3Sync.Models;

namespace AWSS3Sync.Services
{
    public class S3Service : IDisposable
    {
        private readonly AmazonS3Client _s3Client;
        private readonly string _bucketName;
        private readonly MetadataService _metadataService;
        private readonly bool _hasValidCredentials;

        public S3Service(UnifiedUser? user = null)
        {
            var config = ConfigurationService.GetConfiguration();
            var awsConfig = new AmazonS3Config { RegionEndpoint = RegionEndpoint.GetBySystemName(config.AWS.Region) };
            
            // Use user's AWS credentials if available, otherwise fall back to config
            if (user?.HasAwsCredentials == true)
            {
                if (!string.IsNullOrEmpty(user.AwsSessionToken))
                {
                    // Use temporary credentials from Cognito
                    var credentials = new Amazon.Runtime.SessionAWSCredentials(
                        user.AwsAccessKeyId!,
                        user.AwsSecretAccessKey!,
                        user.AwsSessionToken);
                    _s3Client = new AmazonS3Client(credentials, awsConfig.RegionEndpoint);
                }
                else
                {
                    // Use permanent credentials
                    _s3Client = new AmazonS3Client(user.AwsAccessKeyId, user.AwsSecretAccessKey, awsConfig.RegionEndpoint);
                }
                _hasValidCredentials = true;
            }
            else
            {
                // Fall back to config-based credentials (legacy support)
                _s3Client = new AmazonS3Client(config.AWS.AccessKey, config.AWS.SecretKey, awsConfig.RegionEndpoint);
                _hasValidCredentials = !string.IsNullOrEmpty(config.AWS.AccessKey) && !string.IsNullOrEmpty(config.AWS.SecretKey);
            }
            
            _bucketName = config.AWS.BucketName;
            _metadataService = new MetadataService(_s3Client, _bucketName);
        }
        
        /// <summary>
        /// Validate that the service has proper AWS credentials
        /// </summary>
        private void ValidateCredentials(string operation = "S3 operation")
        {
            if (!_hasValidCredentials)
            {
                throw new InvalidOperationException(
                    $"Cannot perform {operation}: No valid AWS credentials available. " +
                    "Please authenticate with AWS Cognito or configure AWS credentials.");
            }
        }

        public async Task<List<FileNode>> ListFilesAsync(UserRole userRole, string prefix = "")
        {
            ValidateCredentials("list S3 files");

            var nodes = new List<FileNode>();
            var request = new ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = prefix,
                Delimiter = "/"
            };

            ListObjectsV2Response response = await _s3Client.ListObjectsV2Async(request);

            // Add "sub-folders"
            foreach (var commonPrefix in response.CommonPrefixes)
            {
                if (commonPrefix != null)
                {
                    var parts = commonPrefix.TrimEnd('/').Split('/');
                    var name = parts.LastOrDefault();
                    if (!string.IsNullOrEmpty(name))
                    {
                        nodes.Add(new FileNode(name, commonPrefix, true, 0, DateTime.MinValue, new List<UserRole>()));
                    }
                }
            }

            // Add files
            foreach (var obj in response.S3Objects)
            {
                if (obj.Key == prefix) continue; // Don't add the directory itself as a file

                var accessRoles = await _metadataService.GetFileAccessRolesAsync(obj.Key);
                // Here we assume that CanUserAccessFile works correctly with the S3FileItem.
                var item = new S3FileItem { Key = obj.Key, Size = obj.Size, LastModified = obj.LastModified, AccessRoles = accessRoles };
                if (CanUserAccessFile(userRole, item))
                {
                    var parts = obj.Key.TrimEnd('/').Split('/');
                    var name = parts.LastOrDefault();
                    if (!string.IsNullOrEmpty(name))
                    {
                        nodes.Add(new FileNode(name, obj.Key, false, obj.Size, obj.LastModified, accessRoles));
                    }
                }
            }

            return nodes.OrderBy(n => n.IsDirectory ? 0 : 1).ThenBy(n => n.Name).ToList();
        }

        private async Task<List<S3FileItem>> GetFlatS3FileList(UserRole userRole)
        {
            var files = new List<S3FileItem>();
            var request = new ListObjectsV2Request { BucketName = _bucketName, MaxKeys = 1000 };
            ListObjectsV2Response response;
            do
            {
                response = await _s3Client.ListObjectsV2Async(request);

                // Add "sub-folders"
                foreach (var commonPrefix in response.CommonPrefixes)
                {
                    files.Add(new S3FileItem { Key = commonPrefix, Size = 0, LastModified = DateTime.MinValue, AccessRoles = new List<UserRole>() });
                }

                // Add files
                foreach (var obj in response.S3Objects)
                {
                    if (obj.Key.StartsWith("logs/", StringComparison.OrdinalIgnoreCase)) continue;

                    var accessRoles = await _metadataService.GetFileAccessRolesAsync(obj.Key);
                    var item = new S3FileItem
                    {
                        Key = obj.Key,
                        Size = obj.Size,
                        LastModified = obj.LastModified,
                        AccessRoles = accessRoles
                    };
                    files.Add(item);
                }

                request.ContinuationToken = response.NextContinuationToken;
            } while (response.IsTruncated);

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

        /// <summary>
        /// Gets the metadata attributes (LastModified and Size) for an S3 object.
        /// </summary>
        /// <param name="key">The key of the S3 object.</param>
        /// <returns>An <see cref="S3ObjectAttributes"/> object, or <c>null</c> if the object is not found.</returns>
        private async Task<S3ObjectAttributes?> GetS3ObjectAttributesAsync(string key)
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
            ValidateCredentials("upload file to S3");
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
            ValidateCredentials("upload directory to S3");
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

        public async Task<string> DownloadFileAsync(string s3Key, string localDirectory, string? versionId = null)
        {
            ValidateCredentials("download file from S3");
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

            if (!string.IsNullOrEmpty(versionId))
            {
                request.VersionId = versionId;
            }

            using (var response = await _s3Client.GetObjectAsync(request))
            using (var responseStream = response.ResponseStream)
            using (var fileStream = File.Create(fullPath))
            {
                await responseStream.CopyToAsync(fileStream);
            }
            return fullPath;
        }

        public async Task DeleteFileAsync(string s3Key, UserRole userRole)
        {
            ValidateCredentials("delete file from S3");
            if (userRole != UserRole.Administrator)
            {
                throw new InvalidOperationException("Only administrators can delete files.");
            }

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

        public async Task<List<FileNode>> ListAllFilesAsync(string prefix, UserRole userRole)
        {
            var files = new List<FileNode>();
            var request = new ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = prefix
            };

            ListObjectsV2Response response;
            do
            {
                response = await _s3Client.ListObjectsV2Async(request);
                foreach (var obj in response.S3Objects)
                {
                    if (obj.Key == prefix || obj.Key.StartsWith("logs/", StringComparison.OrdinalIgnoreCase)) continue;

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
                        var parts = obj.Key.TrimEnd('/').Split('/');
                        var name = parts.LastOrDefault();
                        if (!string.IsNullOrEmpty(name))
                        {
                            files.Add(new FileNode(name, obj.Key, obj.Key.EndsWith("/"), obj.Size, obj.LastModified, accessRoles));
                        }
                    }
                }
                request.ContinuationToken = response.NextContinuationToken;
            } while (response.IsTruncated);

            return files;
        }

        public async Task<List<FileNode>> GetFileVersionsAsync(string key)
        {
            var versions = new List<FileNode>();
            var request = new ListVersionsRequest
            {
                BucketName = _bucketName,
                Prefix = key
            };

            ListVersionsResponse response;
            do
            {
                response = await _s3Client.ListVersionsAsync(request);
                foreach (var version in response.Versions)
                {
                    if (version.Key != key) continue;

                    var node = new FileNode(
                        version.Key,
                        version.Key,
                        false,
                        version.Size,
                        version.LastModified,
                        new List<UserRole>()
                    );
                    node.VersionId = version.VersionId;
                    versions.Add(node);
                }
                request.KeyMarker = response.NextKeyMarker;
                request.VersionIdMarker = response.NextVersionIdMarker;
            } while (response.IsTruncated);

            return versions;
        }

        public void Dispose()
        {
            _s3Client?.Dispose();
        }
    }
}

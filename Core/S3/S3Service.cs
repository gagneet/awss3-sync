using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq; // Added for .Any() in ListBucketItemsAsync
using System.Threading.Tasks;
// Ensure AWSS3Sync.Core.Utils.Misc is accessible if GetContentType is used internally,
// or pass contentType as a parameter to relevant methods.
// For now, we assume it will be called from UI layer which can use Misc directly.

namespace AWSS3Sync.Core.S3
{
    public class S3Service
    {
        private readonly AmazonS3Client _s3Client;
        private readonly string _myBucketName;
        private readonly string _backupS3FolderName = "backup-s3"; // Used in BackupS3ObjectAsync

        public S3Service()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true) // Ensure it's copied to output
                .Build();

            var awsOptions = configuration.GetSection("AWS");

            if (awsOptions == null)
            {
                throw new InvalidOperationException("AWS configuration section is missing in appsettings.json");
            }

            _s3Client = new AmazonS3Client(
                awsOptions["AccessKey"],
                awsOptions["SecretKey"],
                Amazon.RegionEndpoint.GetBySystemName(awsOptions["Region"])
            );

            _myBucketName = awsOptions["BucketName"];
            if (string.IsNullOrEmpty(_myBucketName))
            {
                throw new InvalidOperationException("BucketName is missing in AWS configuration.");
            }
        }

        // Method 1: Based on ListS3ObjectsAsync from frmS3Sync.cs
        public async Task<Dictionary<string, S3Object>> ListS3ObjectsMetadataAsync(string bucketName = null)
        {
            var s3Objects = new Dictionary<string, S3Object>();
            string continuationToken = null;
            string targetBucket = bucketName ?? _myBucketName;

            do
            {
                var request = new ListObjectsV2Request
                {
                    BucketName = targetBucket,
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

        // Method 2: Based on GetObjectsMetadataFromS3Async from frmS3Sync.cs
        public async Task<Dictionary<string, DateTime>> GetS3ObjectsLastModifiedAsync(string bucketName = null)
        {
            var s3ObjectsMetadata = new Dictionary<string, DateTime>();
            string targetBucket = bucketName ?? _myBucketName;
            var request = new ListObjectsRequest { BucketName = targetBucket };
            ListObjectsResponse response;
            do
            {
                response = await _s3Client.ListObjectsAsync(request);
                foreach (var s3Object in response.S3Objects)
                {
                    var metadataRequest = new GetObjectMetadataRequest
                    {
                        BucketName = targetBucket,
                        Key = s3Object.Key
                    };
                    var metadataResponse = await _s3Client.GetObjectMetadataAsync(metadataRequest);
                    s3ObjectsMetadata[s3Object.Key] = metadataResponse.LastModified.ToUniversalTime();
                }
                request.Marker = response.NextMarker;
            } while (response.IsTruncated);
            return s3ObjectsMetadata;
        }

        // Method 3: Based on UploadFileToS3Async from frmS3Sync.cs
        public async Task UploadFileAsync(string filePath, string s3KeyName, string bucketName = null, string contentType = null, S3CannedACL acl = null, List<Tag> objectTags = null)
        {
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                var putObjectRequest = new PutObjectRequest
                {
                    BucketName = bucketName ?? _myBucketName,
                    Key = s3KeyName,
                    InputStream = fileStream,
                    ContentType = contentType, // Pass null if not needed, or handle default
                    CannedACL = acl ?? S3CannedACL.Private, // Default to private if not specified
                    AutoCloseStream = true
                };

                if (objectTags != null && objectTags.Any())
                {
                    putObjectRequest.TagSet = objectTags;
                }

                await _s3Client.PutObjectAsync(putObjectRequest);
            }
        }

        public async Task SetObjectTaggingAsync(string s3KeyName, List<Tag> tags, string bucketName = null)
        {
            if (tags == null)
            {
                // S3 requires a non-null tag set for PutObjectTagging.
                // If the intention is to remove all tags, an empty list should be passed.
                // Or, consider throwing an ArgumentNullException if tags are required.
                // For now, let's assume an empty list means remove tags.
                tags = new List<Tag>(); 
            }

            var putTaggingRequest = new PutObjectTaggingRequest
            {
                BucketName = bucketName ?? _myBucketName,
                Key = s3KeyName,
                Tagging = new Tagging
                {
                    TagSet = tags
                }
            };

            await _s3Client.PutObjectTaggingAsync(putTaggingRequest);
        }

        public async Task<GetObjectTaggingResponse> GetObjectTaggingAsync(string s3KeyName, string bucketName = null)
        {
            var getTaggingRequest = new GetObjectTaggingRequest
            {
                BucketName = bucketName ?? _myBucketName,
                Key = s3KeyName
            };

            return await _s3Client.GetObjectTaggingAsync(getTaggingRequest);
        }

        public async Task ApplyTagsRecursivelyAsync(string s3Prefix, List<Tag> tags, string bucketName = null)
        {
            string targetBucket = bucketName ?? _myBucketName;

            // Ensure prefix ends with a slash if it's meant to be a folder
            if (!string.IsNullOrEmpty(s3Prefix) && !s3Prefix.EndsWith("/"))
            {
                s3Prefix += "/";
            }

            // List all objects under the prefix. ListS3ObjectsMetadataAsync handles pagination.
            // We need to ensure we get all objects if the prefix itself is deep.
            // The existing ListS3ObjectsMetadataAsync lists all objects in the bucket if prefix is null or empty,
            // but for actual recursive tagging, we need to list objects *under* the given prefix.
            // The ListObjectsV2Request in ListS3ObjectsMetadataAsync needs to be adapted or a new listing method created for prefixes.

            // For now, let's assume ListS3ObjectsMetadataAsync can be used if we iterate and filter client-side,
            // or ideally, it should accept a prefix.
            // Re-checking ListS3ObjectsMetadataAsync: it doesn't take a prefix for filtering directly in its current form.
            // It lists all and then the caller would filter. This is inefficient for large buckets.
            // A better ListObjectsV2Request would set the Prefix property.

            // Let's define a more specific listing method for this, or modify ListS3ObjectsMetadataAsync.
            // For now, creating a temporary specific list method here for clarity.
            // Ideally, this listing logic would be part of a more generic, prefix-aware listing method.

            var objectsToTag = new List<S3Object>();
            string continuationToken = null;

            do
            {
                var listRequest = new ListObjectsV2Request
                {
                    BucketName = targetBucket,
                    Prefix = s3Prefix, // Use the S3 prefix capability
                    ContinuationToken = continuationToken
                };
                var listResponse = await _s3Client.ListObjectsV2Async(listRequest);
                
                objectsToTag.AddRange(listResponse.S3Objects);
                
                continuationToken = listResponse.NextContinuationToken;
            } while (continuationToken != null);


            foreach (var s3Object in objectsToTag)
            {
                // Don't try to tag the folder placeholder itself if it appears in the list
                // (S3 doesn't really have folder objects for tagging in the same way as files)
                if (s3Object.Key.EndsWith("/") && s3Object.Size == 0) 
                {
                    continue; 
                }
                await SetObjectTaggingAsync(s3Object.Key, tags, targetBucket);
                Console.WriteLine($"Tagged: {s3Object.Key}"); // For logging/debugging
            }
        }
        
        // Method 4: Based on MoveS3ObjectToBackup from frmS3Sync.cs
        // This version moves to a subfolder within the same bucket, as per btnMoveToBackup_Click
        public async Task BackupS3ObjectAsync(string sourceKey, string bucketName = null)
        {
            string targetBucket = bucketName ?? _myBucketName;
            string backupKey = $"{_backupS3FolderName}/{sourceKey}"; // Matches logic in btnMoveToBackup_Click

            var copyRequest = new CopyObjectRequest
            {
                SourceBucket = targetBucket,
                SourceKey = sourceKey,
                DestinationBucket = targetBucket, // Backup to the same bucket but different key
                DestinationKey = backupKey
            };
            await _s3Client.CopyObjectAsync(copyRequest);
            await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = targetBucket,
                Key = sourceKey
            });
        }

        // Method 5: For downloading a file, based on btnDownloadFiles_Click
        public async Task<Stream> DownloadFileAsStreamAsync(string fileKey, string bucketName = null)
        {
            GetObjectRequest request = new GetObjectRequest
            {
                BucketName = bucketName ?? _myBucketName,
                Key = fileKey,
            };
            GetObjectResponse response = await _s3Client.GetObjectAsync(request);
            return response.ResponseStream; // Caller is responsible for disposing the stream
        }

        // Method 6: Listing S3 items with an exclude list, based on btnListS3Files_Click
        public async Task<List<string>> ListBucketItemsAsync(List<string> excludeList = null, string bucketName = null)
        {
            var items = new List<string>();
            var listObjectsV2Request = new ListObjectsV2Request
            {
                BucketName = bucketName ?? _myBucketName
            };
            var response = await _s3Client.ListObjectsV2Async(listObjectsV2Request);
            if (response.S3Objects != null)
            {
                foreach (var s3Object in response.S3Objects)
                {
                    bool isExcluded = excludeList?.Any(excludedPath => s3Object.Key.StartsWith(excludedPath, StringComparison.OrdinalIgnoreCase)) ?? false;
                    if (!isExcluded)
                    {
                        items.Add(s3Object.Key);
                    }
                }
            }
            return items;
        }

        // Method 7: Sync logic from btnSyncFiles_Click in frmS3Sync.cs
        // This method takes a list of local files and the base path to calculate relative paths
        public async Task SyncLocalFilesToS3Async(IEnumerable<string> localFilePaths, string localBaseFolderPath, int daysToConsider = 60, string bucketName = null, List<Tag> objectTags = null)
        {
            string targetBucket = bucketName ?? _myBucketName;
            var existingS3Objects = await ListS3ObjectsMetadataAsync(targetBucket); // Uses Method 1

            foreach (string filePath in localFilePaths)
            {
                string relativePath = GetRelativePath(localBaseFolderPath, filePath).Replace("\\", "/");
                DateTime localFileLastModified = File.GetLastWriteTimeUtc(filePath);
                bool shouldUpload = false;

                if (existingS3Objects.TryGetValue(relativePath, out var s3ObjectMetadata))
                {
                    if (localFileLastModified > s3ObjectMetadata.LastModified)
                    {
                        shouldUpload = true;
                    }
                }
                else
                {
                    shouldUpload = true; // File does not exist in S3
                }

                // Original logic also had: if (localFileLastModified > DateTime.UtcNow.AddDays(-days)) shouldUpload = true;
                // This means it uploads if EITHER it's newer than S3 OR it was modified recently.
                // Consolidating this:
                if (!shouldUpload && localFileLastModified > DateTime.UtcNow.AddDays(-daysToConsider))
                {
                    shouldUpload = true;
                }

                if (shouldUpload)
                {
                    // ContentType would need to be determined here if desired, e.g. using Misc.GetContentType
                    // For now, passing null. The UI layer can fetch it.
                    // Pass the objectTags to the UploadFileAsync method
                    await UploadFileAsync(filePath, relativePath, targetBucket, contentType: null, acl: null, objectTags: objectTags); 
                    Console.WriteLine($"Uploaded via SyncLocalFilesToS3Async: {relativePath}");
                }
            }
        }

        // Method 8: Core folder upload logic from btnUploadFolder_Click in frmS3Sync.cs
        public async Task UploadFolderContentsAsync(IEnumerable<string> localFilePaths, string localBaseFolderPath, string bucketName = null, List<Tag> objectTags = null)
        {
            string targetBucket = bucketName ?? _myBucketName;
            foreach (string filePath in localFilePaths)
            {
                string relativePath = GetRelativePath(localBaseFolderPath, filePath).Replace("\\", "/");
                // ContentType determination would be needed here if desired.
                // Pass the objectTags to the UploadFileAsync method
                await UploadFileAsync(filePath, relativePath, targetBucket, contentType: null, acl: null, objectTags: objectTags); 
                Console.WriteLine($"Uploaded via UploadFolderContentsAsync: {relativePath}");
            }
        }
        
        // Method 9: Sync logic from SyncFilesToS3Async in frmS3SyncUpload.cs
        // This seems more comprehensive, including backup of remote files not present locally.
        public async Task AdvancedSyncLocalToS3Async(string localSyncFolderPath, IEnumerable<string> localFilesInFolder, string bucketName = null)
        {
            string targetBucket = bucketName ?? _myBucketName;
            string backupBucketForRemote = $"{targetBucket}-backup"; // As per original logic

            if (!Directory.Exists(localSyncFolderPath))
            {
                Console.WriteLine($"Local folder '{localSyncFolderPath}' does not exist for AdvancedSync.");
                return; // Or throw exception
            }

            var s3ObjectsMetadata = await GetS3ObjectsLastModifiedAsync(targetBucket); // Uses Method 2
            var localFilesSet = new HashSet<string>();

            foreach (var filePath in localFilesInFolder) // Expects full paths
            {
                string fileName = Path.GetFileName(filePath); // Original used only filename as key
                string s3Key = fileName; // This might need adjustment if relative paths are desired like other sync.
                                         // For now, sticking to original frmS3SyncUpload logic.

                localFilesSet.Add(s3Key);

                if (!s3ObjectsMetadata.ContainsKey(s3Key))
                {
                    await UploadFileAsync(filePath, s3Key, targetBucket); // Uses Method 3
                    Console.WriteLine($"AdvancedSync: Uploaded new file: {s3Key}");
                }
                else
                {
                    DateTime localFileLastModified = File.GetLastWriteTimeUtc(filePath);
                    DateTime s3ObjectLastModified = s3ObjectsMetadata[s3Key];

                    if (localFileLastModified > s3ObjectLastModified)
                    {
                        await UploadFileAsync(filePath, s3Key, targetBucket); // Uses Method 3
                        Console.WriteLine($"AdvancedSync: Uploaded updated file: {s3Key}");
                    }
                    else
                    {
                        Console.WriteLine($"AdvancedSync: File in S3 is up to date: {s3Key}");
                    }
                }
            }

            // Original logic for moving files not present locally to a different backup *bucket*
            // The method MoveS3ObjectToBackup in frmS3Sync.cs (now BackupS3ObjectAsync) moves to a subfolder.
            // Reconciling this: The frmS3SyncUpload's SyncFilesToS3Async used `MoveS3ObjectToBackup(bucketName, s3Key, backupBucketName);`
            // This implies a different signature or handling for backup.
            // For now, let's assume backup is to a subfolder in the *same* bucket for consistency.
            // If a separate backup bucket is strictly required, S3Service needs modification or another method.
            // The `MoveS3ObjectToBackup` from `frmS3Sync.cs` (which became `BackupS3ObjectAsync`) is what was used by `frmS3SyncUpload.cs`
            // via `await MoveS3ObjectToBackup(bucketName, s3Key, backupBucketName);`
            // This is confusing as `MoveS3ObjectToBackup` in `frmS3Sync.cs` takes 3 params,
            // but the one I planned to create from `btnMoveToBackup_Click` takes 2.
            // Let's stick to the 3-parameter version for this specific advanced sync,
            // and call it something like `MoveObjectToAnotherBucketAsync` if needed.
            // For now, I'll use the existing BackupS3ObjectAsync which moves to a subfolder.
            // This means the "backupBucketName" part of the original SyncFilesToS3Async (from frmS3SyncUpload)
            // will effectively be ignored by BackupS3ObjectAsync if it only uses _myBucketName.
            // This part needs careful review after this step.
            // For now, let's assume backup to a subfolder in the current bucket.
            
            var s3ServiceInternalBackupBucketName = $"{targetBucket}-backup"; // This is a different bucket name.

            foreach (var s3Key in s3ObjectsMetadata.Keys)
            {
                if (!localFilesSet.Contains(s3Key))
                {
                    // This specific method from frmS3SyncUpload.cs calls a three-parameter version of MoveS3ObjectToBackup
                    // Let's define a specific method for this action to keep it clear.
                    await MoveS3ObjectToDifferentBucketAsync(targetBucket, s3Key, s3ServiceInternalBackupBucketName, s3Key);
                    Console.WriteLine($"AdvancedSync: Moved to backup bucket '{s3ServiceInternalBackupBucketName}': {s3Key}");
                }
            }
        }

        // Helper for AdvancedSyncLocalToS3Async to move to a *different* bucket or different key in another bucket
        public async Task MoveS3ObjectToDifferentBucketAsync(string sourceBucket, string sourceKey, string destBucket, string destKey)
        {
             var copyRequest = new CopyObjectRequest
            {
                SourceBucket = sourceBucket,
                SourceKey = sourceKey,
                DestinationBucket = destBucket,
                DestinationKey = destKey // Can be same or different from sourceKey
            };
            await _s3Client.CopyObjectAsync(copyRequest);
            await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = sourceBucket,
                Key = sourceKey
            });
            Console.WriteLine($"Moved s3://{sourceBucket}/{sourceKey} to s3://{destBucket}/{destKey}");
        }


        // Utility method used by some sync logic, made public static if it doesn't rely on instance state.
        // Or keep it private if only used internally. For now, let's assume it might be useful for UI too.
        public static string GetRelativePath(string relativeTo, string path)
        {
            var fromUri = new Uri(AppendDirectorySeparatorChar(relativeTo));
            var toUri = new Uri(path);

            if (fromUri.Scheme != toUri.Scheme)
            {
                return path; 
            }

            Uri relativeUri = fromUri.MakeRelativeUri(toUri);
            string relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            if (toUri.Scheme.Equals("file", StringComparison.InvariantCultureIgnoreCase))
            {
                relativePath = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }

            return relativePath;
        }
        
        private static string AppendDirectorySeparatorChar(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            if (path.Last() != Path.DirectorySeparatorChar && path.Last() != Path.AltDirectorySeparatorChar)
            {
                return path + Path.DirectorySeparatorChar;
            }
            return path;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Amazon.S3;
using Amazon.S3.Model;
using S3FileManager.Models;

namespace S3FileManager.Services
{
    public class MetadataService
    {
        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName;
        private const string RolesTagKey = "AccessRoles";
        private const string PermissionTagKey = "Permission";
        
        // List to track files that received auto-tagging for admin notification
        private static readonly List<string> _autoTaggedFiles = new List<string>();

        public MetadataService(IAmazonS3 s3Client, string bucketName)
        {
            _s3Client = s3Client;
            _bucketName = bucketName;
        }

        public async Task<List<UserRole>> GetFileAccessRolesAsync(string key)
        {
            try
            {
                var request = new GetObjectTaggingRequest
                {
                    BucketName = _bucketName,
                    Key = key
                };
                var response = await _s3Client.GetObjectTaggingAsync(request);
                var rolesTag = response.Tagging.FirstOrDefault(t => t.Key == RolesTagKey);
                var permissionTag = response.Tagging.FirstOrDefault(t => t.Key == PermissionTagKey);
                
                // Check if Permission tag is missing and add it with "pending" status
                if (permissionTag == null)
                {
                    await AssignDefaultPermissionTagAsync(key, response.Tagging);
                }

                if (rolesTag != null && !string.IsNullOrEmpty(rolesTag.Value))
                {
                    return rolesTag.Value.Split(',')
                        .Select(r => Enum.Parse<UserRole>(r))
                        .ToList();
                }
            }
            catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchTagSet")
            {
                // No tags exist at all, assign both default permission and access roles
                await AssignDefaultPermissionTagAsync(key, new List<Tag>());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An unexpected error occurred while retrieving file permissions for '{key}': {ex.Message}", "Permission Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            // Default to Administrator only if no tags are found or an error occurs
            return new List<UserRole> { UserRole.Administrator };
        }

        public async Task SetFileAccessRolesAsync(string key, List<UserRole> roles)
        {
            List<Tag> currentTags = new List<Tag>();
            try
            {
                var getTagsResponse = await _s3Client.GetObjectTaggingAsync(new GetObjectTaggingRequest
                {
                    BucketName = _bucketName,
                    Key = key
                });
                currentTags = getTagsResponse.Tagging;
            }
            catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchTagSet")
            {
                // It's okay if there are no tags yet.
            }

            // Remove the existing roles tag if it's there
            currentTags.RemoveAll(t => t.Key == RolesTagKey);

            // Add the new roles tag
            currentTags.Add(new Tag
            {
                Key = RolesTagKey,
                Value = string.Join(",", roles.Select(r => r.ToString()))
            });

            // Ensure Permission tag exists - if not, add it with "pending" status
            if (!currentTags.Any(t => t.Key == PermissionTagKey))
            {
                currentTags.Add(new Tag
                {
                    Key = PermissionTagKey,
                    Value = "pending"
                });
                
                // Track this file for admin notification since we're adding a pending tag
                lock (_autoTaggedFiles)
                {
                    if (!_autoTaggedFiles.Contains(key))
                    {
                        _autoTaggedFiles.Add(key);
                    }
                }
            }

            var request = new PutObjectTaggingRequest
            {
                BucketName = _bucketName,
                Key = key,
                Tagging = new Tagging
                {
                    TagSet = currentTags
                }
            };
            await _s3Client.PutObjectTaggingAsync(request);
        }

        public async Task RemoveFileAccessRolesAsync(string key)
        {
            List<Tag> currentTags = new List<Tag>();
            try
            {
                var getTagsResponse = await _s3Client.GetObjectTaggingAsync(new GetObjectTaggingRequest
                {
                    BucketName = _bucketName,
                    Key = key
                });
                currentTags = getTagsResponse.Tagging;
            }
            catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchTagSet")
            {
                // No tags to remove, so we're done.
                return;
            }

            // Remove the roles tag
            currentTags.RemoveAll(t => t.Key == RolesTagKey);

            // Apply the remaining tags
            var request = new PutObjectTaggingRequest
            {
                BucketName = _bucketName,
                Key = key,
                Tagging = new Tagging
                {
                    TagSet = currentTags
                }
            };
            await _s3Client.PutObjectTaggingAsync(request);
        }

        /// <summary>
        /// Assigns default "Permission": "pending" tag to objects that are missing permission tags.
        /// Also tracks the file for admin notification.
        /// </summary>
        private async Task AssignDefaultPermissionTagAsync(string key, List<Tag> existingTags)
        {
            try
            {
                var updatedTags = new List<Tag>(existingTags);
                
                // Add Permission tag if missing
                if (!updatedTags.Any(t => t.Key == PermissionTagKey))
                {
                    updatedTags.Add(new Tag
                    {
                        Key = PermissionTagKey,
                        Value = "pending"
                    });
                }
                
                // Add default AccessRoles if missing
                if (!updatedTags.Any(t => t.Key == RolesTagKey))
                {
                    updatedTags.Add(new Tag
                    {
                        Key = RolesTagKey,
                        Value = UserRole.Administrator.ToString()
                    });
                }

                // Apply the updated tags
                var request = new PutObjectTaggingRequest
                {
                    BucketName = _bucketName,
                    Key = key,
                    Tagging = new Tagging
                    {
                        TagSet = updatedTags
                    }
                };
                await _s3Client.PutObjectTaggingAsync(request);
                
                // Track this file for admin notification
                lock (_autoTaggedFiles)
                {
                    if (!_autoTaggedFiles.Contains(key))
                    {
                        _autoTaggedFiles.Add(key);
                    }
                }
            }
            catch (Exception ex)
            {
                // Don't fail the main operation if auto-tagging fails, just log it
                MessageBox.Show($"Warning: Could not auto-assign permission tag to '{key}': {ex.Message}", 
                    "Auto-Tagging Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// Gets the list of files that have been auto-tagged with pending permissions.
        /// Used by administrators to review and set proper permissions.
        /// </summary>
        public static List<string> GetAutoTaggedFiles()
        {
            lock (_autoTaggedFiles)
            {
                return new List<string>(_autoTaggedFiles);
            }
        }

        /// <summary>
        /// Clears the list of auto-tagged files (typically called after admin review).
        /// </summary>
        public static void ClearAutoTaggedFiles()
        {
            lock (_autoTaggedFiles)
            {
                _autoTaggedFiles.Clear();
            }
        }
    }
}
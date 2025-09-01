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

                if (rolesTag != null && !string.IsNullOrEmpty(rolesTag.Value))
                {
                    return rolesTag.Value.Split(',')
                        .Select(r => Enum.Parse<UserRole>(r))
                        .ToList();
                }
            }
            catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchTagSet")
            {
                // No tags exist, so return default
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
    }
}
using Amazon.S3;
using Amazon.S3.Model;
using FileSyncApp.Core.Models;
using Microsoft.Extensions.Logging;

namespace FileSyncApp.S3.Services;

public class S3MetadataService
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly ILogger<S3MetadataService> _logger;
    private const string RolesTagKey = "AccessRoles";
    private const string PermissionTagKey = "Permission";

    private static readonly List<string> _autoTaggedFiles = new List<string>();

    public S3MetadataService(IAmazonS3 s3Client, string bucketName, ILogger<S3MetadataService> logger)
    {
        _s3Client = s3Client;
        _bucketName = bucketName;
        _logger = logger;
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

            if (permissionTag == null)
            {
                await AssignDefaultPermissionTagAsync(key, response.Tagging);
            }

            if (rolesTag != null && !string.IsNullOrEmpty(rolesTag.Value))
            {
                return rolesTag.Value.Split(',')
                    .Select(r => Enum.TryParse<UserRole>(r, out var role) ? role : UserRole.Administrator)
                    .ToList();
            }
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchTagSet")
        {
            await AssignDefaultPermissionTagAsync(key, new List<Tag>());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve file permissions for {Key}", key);
        }

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
        catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchTagSet") { }

        currentTags.RemoveAll(t => t.Key == RolesTagKey);
        currentTags.Add(new Tag
        {
            Key = RolesTagKey,
            Value = string.Join(",", roles.Select(r => r.ToString()))
        });

        if (!currentTags.Any(t => t.Key == PermissionTagKey))
        {
            currentTags.Add(new Tag { Key = PermissionTagKey, Value = "pending" });
            lock (_autoTaggedFiles)
            {
                if (!_autoTaggedFiles.Contains(key)) _autoTaggedFiles.Add(key);
            }
        }

        await _s3Client.PutObjectTaggingAsync(new PutObjectTaggingRequest
        {
            BucketName = _bucketName,
            Key = key,
            Tagging = new Tagging { TagSet = currentTags }
        });
    }

    private async Task AssignDefaultPermissionTagAsync(string key, List<Tag> existingTags)
    {
        try
        {
            var updatedTags = new List<Tag>(existingTags);
            if (!updatedTags.Any(t => t.Key == PermissionTagKey))
            {
                updatedTags.Add(new Tag { Key = PermissionTagKey, Value = "pending" });
            }
            if (!updatedTags.Any(t => t.Key == RolesTagKey))
            {
                updatedTags.Add(new Tag { Key = RolesTagKey, Value = UserRole.Administrator.ToString() });
            }

            await _s3Client.PutObjectTaggingAsync(new PutObjectTaggingRequest
            {
                BucketName = _bucketName,
                Key = key,
                Tagging = new Tagging { TagSet = updatedTags }
            });

            lock (_autoTaggedFiles)
            {
                if (!_autoTaggedFiles.Contains(key)) _autoTaggedFiles.Add(key);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not auto-assign permission tag to {Key}", key);
        }
    }
}

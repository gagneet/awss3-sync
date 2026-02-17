using System.IO.Compression;
using Amazon.S3;
using Amazon.S3.Model;

namespace FileSyncApp.S3.Services;

public class S3ZipService
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;

    public S3ZipService(IAmazonS3 s3Client, string bucketName)
    {
        _s3Client = s3Client;
        _bucketName = bucketName;
    }

    public async Task CreateZipFromS3Async(IEnumerable<string> keys, Stream destinationStream)
    {
        using var archive = new ZipArchive(destinationStream, ZipArchiveMode.Create, true);
        foreach (var key in keys)
        {
            var getRequest = new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = key
            };

            using var response = await _s3Client.GetObjectAsync(getRequest);
            var entry = archive.CreateEntry(key);
            using var entryStream = entry.Open();
            await response.ResponseStream.CopyToAsync(entryStream);
        }
    }
}

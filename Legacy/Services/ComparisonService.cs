using System.Collections.Generic;
using System.IO;
using System.Linq;
using AWSS3Sync.Models;

namespace AWSS3Sync.Services
{
    public class ComparisonService
    {
        public List<FileComparisonResult> CompareDirectories(
            FileNode localDirectory,
            FileNode s3Directory,
            IEnumerable<FileNode> localFiles,
            IEnumerable<FileNode> s3Files)
        {
            var results = new List<FileComparisonResult>();
            var localFileDict = localFiles.ToDictionary(f => f.Path.Substring(localDirectory.Path.Length + 1));
            var s3FileDict = s3Files.ToDictionary(f => f.Path.Substring(s3Directory.Path.Length + 1));

            // Find local-only and modified files
            foreach (var local in localFileDict)
            {
                if (s3FileDict.TryGetValue(local.Key, out var s3File))
                {
                    // File exists in both, check for modification
                    if (local.Value.LastModified > s3File.LastModified)
                    {
                        results.Add(new FileComparisonResult
                        {
                            RelativePath = local.Key,
                            FileName = local.Value.Name,
                            Status = ComparisonStatus.Modified,
                            LocalFile = local.Value,
                            S3File = s3File
                        });
                    }
                    else
                    {
                        results.Add(new FileComparisonResult
                        {
                            RelativePath = local.Key,
                            FileName = local.Value.Name,
                            Status = ComparisonStatus.Identical,
                            LocalFile = local.Value,
                            S3File = s3File
                        });
                    }
                }
                else
                {
                    // File only exists locally
                    results.Add(new FileComparisonResult
                    {
                        RelativePath = local.Key,
                        FileName = local.Value.Name,
                        Status = ComparisonStatus.LocalOnly,
                        LocalFile = local.Value
                    });
                }
            }

            // Find S3-only files
            foreach (var s3 in s3FileDict)
            {
                if (!localFileDict.ContainsKey(s3.Key))
                {
                    results.Add(new FileComparisonResult
                    {
                        RelativePath = s3.Key,
                        FileName = s3.Value.Name,
                        Status = ComparisonStatus.S3Only,
                        S3File = s3.Value
                    });
                }
            }

            return results;
        }
    }
}

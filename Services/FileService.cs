using System.Collections.Generic;
using System.IO;
using AWSS3Sync.Models;

namespace AWSS3Sync.Services
{
    public class FileService
    {
        public List<LocalFileItem> GetLocalFiles(string path)
        {
            var files = new List<LocalFileItem>();

            if (!Directory.Exists(path))
                return files;

            // Add directories
            foreach (string dir in Directory.GetDirectories(path))
            {
                var dirInfo = new DirectoryInfo(dir);
                files.Add(new LocalFileItem
                {
                    Name = dirInfo.Name,
                    FullPath = dir,
                    IsDirectory = true,
                    Size = 0
                });
            }

            // Add files
            foreach (string file in Directory.GetFiles(path))
            {
                var fileInfo = new FileInfo(file);
                files.Add(new LocalFileItem
                {
                    Name = fileInfo.Name,
                    FullPath = file,
                    IsDirectory = false,
                    Size = fileInfo.Length
                });
            }

            return files;
        }

        public List<FileNode> GetAllFiles(string path)
        {
            var nodes = new List<FileNode>();
            var rootDir = new DirectoryInfo(path);

            foreach (var file in rootDir.GetFiles("*", SearchOption.AllDirectories))
            {
                nodes.Add(new FileNode(
                    file.Name,
                    file.FullName,
                    false,
                    file.Length,
                    file.LastWriteTimeUtc,
                    new List<UserRole>() // Local files don't have roles
                ));
            }

            foreach (var dir in rootDir.GetDirectories("*", SearchOption.AllDirectories))
            {
                nodes.Add(new FileNode(
                    dir.Name,
                    dir.FullName,
                    true,
                    0,
                    dir.LastWriteTimeUtc,
                    new List<UserRole>()
                ));
            }
            return nodes;
        }

        public string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
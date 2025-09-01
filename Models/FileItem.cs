using System;
using System.Collections.Generic;

namespace S3FileManager.Models
{
    public class LocalFileItem
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public long Size { get; set; }
        public List<UserRole> AccessRoles { get; set; } = new List<UserRole>();
    }

    public class S3FileItem
    {
        public string Key { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public List<UserRole> AccessRoles { get; set; } = new List<UserRole>();
        public bool IsDirectory => Key.EndsWith("/");
        public string DisplayName => Key.Contains('/') ? Key : System.IO.Path.GetFileName(Key);
    }

    public class FileNode
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public bool IsDirectory { get; set; }
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public List<UserRole> AccessRoles { get; set; } = new List<UserRole>();
        public List<FileNode> Children { get; set; } = new List<FileNode>();
        public bool IsS3 { get; set; }

        // Constructor for local files
        public FileNode(string name, string path, bool isDirectory, long size, DateTime lastModified)
        {
            Name = name;
            Path = path;
            IsDirectory = isDirectory;
            Size = size;
            LastModified = lastModified;
            IsS3 = false;
        }

        // Constructor for S3 files
        public FileNode(string name, string path, bool isDirectory, long size, DateTime lastModified, List<UserRole> accessRoles)
        {
            Name = name;
            Path = path;
            IsDirectory = isDirectory;
            Size = size;
            LastModified = lastModified;
            AccessRoles = accessRoles;
            IsS3 = true;
        }
    }
}
using System;
using System.Collections.Generic;

namespace S3FileManager.Models
{
    public class LocalFileItem
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public bool IsDirectory { get; set; }
        public long Size { get; set; }
        public List<UserRole> AccessRoles { get; set; } = new List<UserRole>();
    }

    public class S3FileItem
    {
        public string Key { get; set; }
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public List<UserRole> AccessRoles { get; set; } = new List<UserRole>();
        public bool IsDirectory => Key.EndsWith("/");
        public string DisplayName => Key.Contains('/') ? Key : System.IO.Path.GetFileName(Key);
    }
}
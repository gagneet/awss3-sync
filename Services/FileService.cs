﻿using System.Collections.Generic;
using System.IO;
using S3FileManager.Models;

namespace S3FileManager.Services
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
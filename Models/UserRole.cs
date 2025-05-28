using System;

namespace S3FileManager.Models
{
    public enum UserRole
    {
        User,
        Executive,
        Administrator
    }

    public class User
    {
        public string Username { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public DateTime LastLogin { get; set; }
    }
}
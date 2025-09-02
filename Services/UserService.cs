using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using Newtonsoft.Json;
using AWSS3Sync.Models;

namespace AWSS3Sync.Services
{
    public class UserService
    {
        private readonly string _usersFilePath;
        private List<UserCredentials> _users = new List<UserCredentials>();

        public UserService()
        {
            _usersFilePath = Path.Combine(Application.StartupPath, "users.json");
            LoadUsers();
        }

        private void LoadUsers()
        {
            if (File.Exists(_usersFilePath))
            {
                var json = File.ReadAllText(_usersFilePath);
                _users = JsonConvert.DeserializeObject<List<UserCredentials>>(json) ?? new List<UserCredentials>();
            }
            else
            {
                // Create a default user file if it doesn't exist
                _users = new List<UserCredentials>
                {
                    new UserCredentials { Username = "admin", PasswordHash = HashPassword("admin"), Role = UserRole.Administrator },
                    new UserCredentials { Username = "exec", PasswordHash = HashPassword("exec"), Role = UserRole.Executive },
                    new UserCredentials { Username = "user", PasswordHash = HashPassword("user"), Role = UserRole.User }
                };
                SaveUsers();
            }
        }

        private void SaveUsers()
        {
            var json = JsonConvert.SerializeObject(_users, Formatting.Indented);
            File.WriteAllText(_usersFilePath, json);
        }

        public User? ValidateUser(string username, string password)
        {
            var userCredentials = _users.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

            if (userCredentials != null)
            {
                var passwordHash = HashPassword(password);
                if (passwordHash == userCredentials.PasswordHash)
                {
                    return new User
                    {
                        Username = userCredentials.Username,
                        Role = userCredentials.Role,
                        LastLogin = DateTime.Now
                    };
                }
            }

            return null;
        }

        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                var builder = new StringBuilder();
                foreach (var b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }

    public class UserCredentials
    {
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public UserRole Role { get; set; }
    }
}

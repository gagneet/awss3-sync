using System.Security.Cryptography;
using System.Text;
using FileSyncApp.Core.Interfaces;
using FileSyncApp.Core.Models;
using Newtonsoft.Json;

namespace FileSyncApp.Core.Services;

public class LocalAuthService : IAuthService
{
    private readonly string _usersFilePath;
    private List<UserCredentials> _users = new();
    private UnifiedUser? _currentUser;

    public LocalAuthService(string usersFilePath = "users.json")
    {
        _usersFilePath = usersFilePath;
        LoadUsers();
    }

    private void LoadUsers()
    {
        if (File.Exists(_usersFilePath))
        {
            try
            {
                var json = File.ReadAllText(_usersFilePath);
                _users = JsonConvert.DeserializeObject<List<UserCredentials>>(json) ?? new List<UserCredentials>();
            }
            catch { InitializeDefaultUsers(); }
        }
        else
        {
            InitializeDefaultUsers();
        }
    }

    private void InitializeDefaultUsers()
    {
        _users = new List<UserCredentials>
        {
            new UserCredentials { Username = "admin", PasswordHash = HashPassword("admin"), Role = UserRole.Administrator },
            new UserCredentials { Username = "exec", PasswordHash = HashPassword("exec"), Role = UserRole.Executive },
            new UserCredentials { Username = "user", PasswordHash = HashPassword("user"), Role = UserRole.User }
        };
        SaveUsers();
    }

    private void SaveUsers()
    {
        var json = JsonConvert.SerializeObject(_users, Formatting.Indented);
        File.WriteAllText(_usersFilePath, json);
    }

    public async Task<UnifiedUser?> AuthenticateAsync(string username, string password, bool forceOnline = false)
    {
        await Task.CompletedTask;
        var userCreds = _users.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

        if (userCreds != null)
        {
            var passwordHash = HashPassword(password);
            if (passwordHash == userCreds.PasswordHash)
            {
                _currentUser = new UnifiedUser
                {
                    Username = userCreds.Username,
                    Role = userCreds.Role,
                    LastLogin = DateTime.UtcNow,
                    AuthType = AuthenticationType.Local,
                    IsOfflineMode = false
                };
                return _currentUser;
            }
        }

        return null;
    }

    public Task<bool> RefreshTokensAsync() => Task.FromResult(true);

    public Task SignOutAsync()
    {
        _currentUser = null;
        return Task.CompletedTask;
    }

    public UnifiedUser? GetCurrentUser() => _currentUser;

    private string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        var builder = new StringBuilder();
        foreach (var b in bytes)
        {
            builder.Append(b.ToString("x2"));
        }
        return builder.ToString();
    }
}

public class UserCredentials
{
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
}

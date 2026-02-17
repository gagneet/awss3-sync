using FileSyncApp.Core.Models;

namespace FileSyncApp.Core.Models;

public class CachedCredentials
{
    public string Username { get; set; } = string.Empty;
    public string EncryptedPassword { get; set; } = string.Empty;
    public string EncryptedRefreshToken { get; set; } = string.Empty;
    public UserRole CachedRole { get; set; }
    public DateTime LastSuccessfulLogin { get; set; }
    public DateTime CacheExpiry { get; set; }

    public bool IsValid()
    {
        return DateTime.UtcNow < CacheExpiry;
    }
}

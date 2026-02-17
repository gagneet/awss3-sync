using FileSyncApp.Core.Models;

namespace FileSyncApp.Core.Interfaces;

public interface IAuthService
{
    Task<UnifiedUser?> AuthenticateAsync(string username, string password, bool forceOnline = false);
    Task<bool> RefreshTokensAsync();
    Task SignOutAsync();
    UnifiedUser? GetCurrentUser();
}

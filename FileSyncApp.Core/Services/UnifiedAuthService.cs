using FileSyncApp.Core.Interfaces;
using FileSyncApp.Core.Models;

namespace FileSyncApp.Core.Services;

public enum AuthMode { Cognito, Local }

public class UnifiedAuthService : IAuthService
{
    private readonly IAuthService _cognitoService;
    private readonly IAuthService _localService;
    private AuthMode _currentMode = AuthMode.Cognito;

    public UnifiedAuthService(IAuthService cognitoService, IAuthService localService)
    {
        _cognitoService = cognitoService;
        _localService = localService;
    }

    public AuthMode CurrentMode
    {
        get => _currentMode;
        set => _currentMode = value;
    }

    public async Task<UnifiedUser?> AuthenticateAsync(string username, string password, bool forceOnline = false)
    {
        if (_currentMode == AuthMode.Cognito)
        {
            return await _cognitoService.AuthenticateAsync(username, password, forceOnline);
        }
        else
        {
            return await _localService.AuthenticateAsync(username, password, forceOnline);
        }
    }

    public async Task<bool> RefreshTokensAsync()
    {
        return _currentMode == AuthMode.Cognito
            ? await _cognitoService.RefreshTokensAsync()
            : await _localService.RefreshTokensAsync();
    }

    public async Task SignOutAsync()
    {
        await _cognitoService.SignOutAsync();
        await _localService.SignOutAsync();
    }

    public UnifiedUser? GetCurrentUser()
    {
        return _currentMode == AuthMode.Cognito
            ? _cognitoService.GetCurrentUser()
            : _localService.GetCurrentUser();
    }
}

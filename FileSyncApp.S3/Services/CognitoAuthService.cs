using System.Security.Cryptography;
using System.Text;
using Amazon;
using Amazon.CognitoIdentity;
using Amazon.CognitoIdentity.Model;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.Extensions.CognitoAuthentication;
using Amazon.Runtime;
using FileSyncApp.Core.Interfaces;
using FileSyncApp.Core.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace FileSyncApp.S3.Services;

public class CognitoAuthService : IAuthService, IDisposable
{
    private readonly CognitoConfig _config;
    private readonly AmazonCognitoIdentityProviderClient? _cognitoClient;
    private readonly ICredentialService _credentialService;
    private readonly ILogger<CognitoAuthService> _logger;
    private UnifiedUser? _currentUser;

    public CognitoAuthService(
        IConfigurationService configService,
        ICredentialService credentialService,
        ILogger<CognitoAuthService> logger)
    {
        var appConfig = configService.GetConfiguration();
        _config = appConfig.Cognito;
        _credentialService = credentialService;
        _logger = logger;

        if (!string.IsNullOrEmpty(_config.Region))
        {
            var cognitoConfig = new AmazonCognitoIdentityProviderConfig
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(_config.Region)
            };

            _cognitoClient = new AmazonCognitoIdentityProviderClient(
                new AnonymousAWSCredentials(),
                cognitoConfig);
        }
    }

    private bool IsOfflineMode()
    {
        if (string.IsNullOrEmpty(_config.Region)) return true;
        try
        {
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var result = client.GetAsync($"https://cognito-idp.{_config.Region}.amazonaws.com").Result;
            return false;
        }
        catch { return true; }
    }

    public async Task<UnifiedUser?> AuthenticateAsync(string username, string password, bool forceOnline = false)
    {
        if (_cognitoClient == null)
        {
            throw new InvalidOperationException("AWS Cognito is not configured. Please check your appsettings.json.");
        }

        try
        {
            if (!forceOnline && IsOfflineMode())
            {
                return await AuthenticateOfflineAsync(username, password);
            }

            var authRequest = new InitiateAuthRequest
            {
                ClientId = _config.ClientId,
                AuthFlow = AuthFlowType.USER_PASSWORD_AUTH,
                AuthParameters = new Dictionary<string, string>
                {
                    {"USERNAME", username},
                    {"PASSWORD", password}
                }
            };

            if (!string.IsNullOrEmpty(_config.ClientSecret))
            {
                authRequest.AuthParameters.Add("SECRET_HASH",
                    CalculateSecretHash(username, _config.ClientId, _config.ClientSecret));
            }

            var authResponse = await _cognitoClient.InitiateAuthAsync(authRequest);

            if (authResponse.AuthenticationResult != null)
            {
                var userDetails = await GetUserDetailsAsync(authResponse.AuthenticationResult.AccessToken);
                var groups = await GetUserGroupsAsync(username, authResponse.AuthenticationResult.AccessToken);

                _currentUser = new UnifiedUser
                {
                    Username = username,
                    Email = userDetails.GetValueOrDefault("email", ""),
                    CognitoSub = userDetails.GetValueOrDefault("sub", ""),
                    Groups = groups,
                    Role = MapGroupsToRole(groups),
                    LastLogin = DateTime.UtcNow,
                    TokenExpiry = DateTime.UtcNow.AddSeconds((double)(authResponse.AuthenticationResult.ExpiresIn ?? 3600)),
                    AccessToken = authResponse.AuthenticationResult.AccessToken,
                    IdToken = authResponse.AuthenticationResult.IdToken,
                    RefreshToken = authResponse.AuthenticationResult.RefreshToken,
                    AuthType = AuthenticationType.Cognito,
                    IsOfflineMode = false
                };

                await GetAwsCredentialsAsync(_currentUser);

                if (_config.EnableOfflineMode)
                {
                    _credentialService.SaveRefreshToken(username, _currentUser.RefreshToken ?? "");
                }

                return _currentUser;
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication failed");
            if (_config.EnableOfflineMode && !forceOnline)
            {
                return await AuthenticateOfflineAsync(username, password);
            }
            throw;
        }
    }

    private async Task<UnifiedUser?> AuthenticateOfflineAsync(string username, string password)
    {
        await Task.CompletedTask;
        return null;
    }

    public async Task<bool> RefreshTokensAsync()
    {
        if (_cognitoClient == null) return false;

        if (_currentUser == null || string.IsNullOrEmpty(_currentUser.RefreshToken))
        {
            var cached = _credentialService.LoadRefreshToken();
            if (cached == null) return false;
            return false;
        }

        try
        {
            var refreshRequest = new InitiateAuthRequest
            {
                ClientId = _config.ClientId,
                AuthFlow = AuthFlowType.REFRESH_TOKEN_AUTH,
                AuthParameters = new Dictionary<string, string>
                {
                    {"REFRESH_TOKEN", _currentUser.RefreshToken}
                }
            };

            if (!string.IsNullOrEmpty(_config.ClientSecret))
            {
                refreshRequest.AuthParameters.Add("SECRET_HASH",
                    CalculateSecretHash(_currentUser.Username, _config.ClientId, _config.ClientSecret));
            }

            var refreshResponse = await _cognitoClient.InitiateAuthAsync(refreshRequest);

            if (refreshResponse.AuthenticationResult != null)
            {
                _currentUser.AccessToken = refreshResponse.AuthenticationResult.AccessToken;
                _currentUser.IdToken = refreshResponse.AuthenticationResult.IdToken;
                _currentUser.TokenExpiry = DateTime.UtcNow.AddSeconds((double)(refreshResponse.AuthenticationResult.ExpiresIn ?? 3600));
                await GetAwsCredentialsAsync(_currentUser);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token refresh failed");
            return false;
        }
    }

    private async Task GetAwsCredentialsAsync(UnifiedUser user)
    {
        if (string.IsNullOrEmpty(_config.IdentityPoolId) || string.IsNullOrEmpty(user.IdToken))
        {
            return;
        }

        try
        {
            var cognitoIdentityClient = new AmazonCognitoIdentityClient(
                new AnonymousAWSCredentials(),
                RegionEndpoint.GetBySystemName(_config.Region));

            var getIdRequest = new GetIdRequest
            {
                IdentityPoolId = _config.IdentityPoolId,
                Logins = new Dictionary<string, string>
                {
                    {$"cognito-idp.{_config.Region}.amazonaws.com/{_config.UserPoolId}", user.IdToken}
                }
            };

            var getIdResponse = await cognitoIdentityClient.GetIdAsync(getIdRequest);

            var getCredentialsRequest = new GetCredentialsForIdentityRequest
            {
                IdentityId = getIdResponse.IdentityId,
                Logins = getIdRequest.Logins
            };

            var getCredentialsResponse = await cognitoIdentityClient.GetCredentialsForIdentityAsync(getCredentialsRequest);

            user.AwsAccessKeyId = getCredentialsResponse.Credentials.AccessKeyId;
            user.AwsSecretAccessKey = getCredentialsResponse.Credentials.SecretKey;
            user.AwsSessionToken = getCredentialsResponse.Credentials.SessionToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get AWS credentials");
        }
    }

    private async Task<Dictionary<string, string>> GetUserDetailsAsync(string accessToken)
    {
        if (_cognitoClient == null) throw new InvalidOperationException();
        var response = await _cognitoClient.GetUserAsync(new GetUserRequest { AccessToken = accessToken });
        return response.UserAttributes.ToDictionary(a => a.Name, a => a.Value);
    }

    private async Task<List<string>> GetUserGroupsAsync(string username, string accessToken)
    {
        if (_cognitoClient == null) return new List<string>();
        try
        {
            var response = await _cognitoClient.AdminListGroupsForUserAsync(new AdminListGroupsForUserRequest
            {
                Username = username,
                UserPoolId = _config.UserPoolId
            });
            return response.Groups.Select(g => g.GroupName).ToList();
        }
        catch { return new List<string>(); }
    }

    private UserRole MapGroupsToRole(List<string> groups)
    {
        if (groups.Contains("strata-admin")) return UserRole.Administrator;
        if (groups.Contains("strata-ec")) return UserRole.Executive;
        return UserRole.User;
    }

    private string CalculateSecretHash(string username, string clientId, string clientSecret)
    {
        byte[] message = Encoding.UTF8.GetBytes(username + clientId);
        byte[] key = Encoding.UTF8.GetBytes(clientSecret);
        using var hmac = new HMACSHA256(key);
        byte[] hash = hmac.ComputeHash(message);
        return Convert.ToBase64String(hash);
    }

    public async Task SignOutAsync()
    {
        if (_cognitoClient != null && _currentUser != null && !string.IsNullOrEmpty(_currentUser.AccessToken))
        {
            try
            {
                await _cognitoClient.GlobalSignOutAsync(new GlobalSignOutRequest { AccessToken = _currentUser.AccessToken });
            }
            catch { }
        }
        _currentUser = null;
    }

    public UnifiedUser? GetCurrentUser() => _currentUser;

    public void Dispose() => _cognitoClient?.Dispose();
}

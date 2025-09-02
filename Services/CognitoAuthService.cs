using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Amazon;
using Amazon.CognitoIdentity;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.Extensions.CognitoAuthentication;
using Amazon.Runtime;
using Newtonsoft.Json;
using S3FileManager.Models;

namespace S3FileManager.Services
{
    /// <summary>
    /// Service for handling AWS Cognito authentication with offline capabilities
    /// </summary>
    public class CognitoAuthService : IDisposable
    {
        private readonly CognitoConfig _config;
        private readonly AmazonCognitoIdentityProviderClient _cognitoClient;
        private readonly string _cacheFilePath;
        private CognitoUserPool? _userPool;
        private CognitoUser? _currentUser;

        public CognitoAuthService()
        {
            var appConfig = ConfigurationService.GetConfiguration();
            if (appConfig?.Cognito == null)
            {
                throw new InvalidOperationException("Cognito configuration is missing or invalid.");
            }
            _config = appConfig.Cognito;

            if (string.IsNullOrEmpty(_config.Region) || string.IsNullOrEmpty(_config.UserPoolId) || string.IsNullOrEmpty(_config.ClientId))
            {
                throw new InvalidOperationException("Required Cognito configuration properties are missing.");
            }

            // Initialize Cognito client
            var cognitoConfig = new AmazonCognitoIdentityProviderConfig
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(_config.Region)
            };

            try
            {
                _cognitoClient = new AmazonCognitoIdentityProviderClient(
                    new AnonymousAWSCredentials(), 
                    cognitoConfig);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to initialize AWS Cognito client.", ex);
            }
            
            // Setup cache file path
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appFolder = Path.Combine(appDataPath, "StrataS3Manager");
            Directory.CreateDirectory(appFolder);
            _cacheFilePath = Path.Combine(appFolder, "credentials.cache");
            
            // Initialize user pool
            _userPool = new CognitoUserPool(_config.UserPoolId, _config.ClientId, _cognitoClient);
        }

        /// <summary>
        /// Authenticate user with Cognito or use cached credentials for offline access
        /// </summary>
        public async Task<CognitoUser?> AuthenticateAsync(string username, string password, bool forceOnline = false)
        {
            try
            {
                // Try online authentication first if not forced offline
                if (!forceOnline && IsOfflineMode())
                {
                    return await AuthenticateOfflineAsync(username, password);
                }
                
                // Online authentication
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
                
                // Add client secret if configured
                if (!string.IsNullOrEmpty(_config.ClientSecret))
                {
                    authRequest.AuthParameters.Add("SECRET_HASH", 
                        CalculateSecretHash(username, _config.ClientId, _config.ClientSecret));
                }
                
                var authResponse = await _cognitoClient.InitiateAuthAsync(authRequest);
                
                if (authResponse.AuthenticationResult != null)
                {
                    // Get user details and groups
                    var userDetails = await GetUserDetailsAsync(authResponse.AuthenticationResult.AccessToken);
                    var groups = await GetUserGroupsAsync(username, authResponse.AuthenticationResult.AccessToken);
                    
                    _currentUser = new CognitoUser
                    {
                        Username = username,
                        Email = userDetails.GetValueOrDefault("email", ""),
                        Sub = userDetails.GetValueOrDefault("sub", ""),
                        Groups = groups,
                        Role = CognitoUser.MapGroupsToRole(groups),
                        LastLogin = DateTime.UtcNow,
                        TokenExpiry = DateTime.UtcNow.AddSeconds(authResponse.AuthenticationResult.ExpiresIn),
                        AccessToken = authResponse.AuthenticationResult.AccessToken,
                        IdToken = authResponse.AuthenticationResult.IdToken,
                        RefreshToken = authResponse.AuthenticationResult.RefreshToken,
                        IsOfflineMode = false
                    };
                    
                    // Get temporary AWS credentials for S3 access
                    await GetAwsCredentialsAsync(_currentUser);
                    
                    // Cache credentials for offline use if enabled
                    if (_config.EnableOfflineMode)
                    {
                        await CacheCredentialsAsync(_currentUser, password);
                    }
                    
                    return _currentUser;
                }
                
                // Handle MFA or other challenges if needed
                if (authResponse.ChallengeName != null)
                {
                    throw new NotImplementedException($"Authentication challenge {authResponse.ChallengeName} not yet implemented");
                }
                
                return null;
            }
            catch (Exception ex)
            {
                // If online authentication fails, try offline if enabled
                if (_config.EnableOfflineMode && !forceOnline)
                {
                    return await AuthenticateOfflineAsync(username, password);
                }
                
                throw new Exception($"Authentication failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Authenticate using cached credentials for offline access
        /// </summary>
        private async Task<CognitoUser?> AuthenticateOfflineAsync(string username, string password)
        {
            try
            {
                var cachedCredentials = await LoadCachedCredentialsAsync();
                
                if (cachedCredentials == null || cachedCredentials.Username != username)
                {
                    return null;
                }
                
                // Verify password hash
                string passwordHash = HashPassword(password);
                if (cachedCredentials.EncryptedPassword != passwordHash)
                {
                    return null;
                }
                
                // Check if cache is still valid
                if (!cachedCredentials.IsValid())
                {
                    return null;
                }
                
                _currentUser = new CognitoUser
                {
                    Username = username,
                    Role = cachedCredentials.CachedRole,
                    LastLogin = DateTime.UtcNow,
                    IsOfflineMode = true,
                    CachedCredentials = cachedCredentials
                };
                
                return _currentUser;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Refresh authentication tokens
        /// </summary>
        public async Task<bool> RefreshTokensAsync()
        {
            if (_currentUser == null || string.IsNullOrEmpty(_currentUser.RefreshToken))
            {
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
                    _currentUser.TokenExpiry = DateTime.UtcNow.AddSeconds(refreshResponse.AuthenticationResult.ExpiresIn);
                    
                    // Refresh AWS credentials
                    await GetAwsCredentialsAsync(_currentUser);
                    
                    return true;
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get temporary AWS credentials using Cognito Identity
        /// </summary>
        private async Task GetAwsCredentialsAsync(CognitoUser user)
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
                
                // Get identity ID
                var getIdRequest = new GetIdRequest
                {
                    IdentityPoolId = _config.IdentityPoolId,
                    Logins = new Dictionary<string, string>
                    {
                        {$"cognito-idp.{_config.Region}.amazonaws.com/{_config.UserPoolId}", user.IdToken}
                    }
                };
                
                var getIdResponse = await cognitoIdentityClient.GetIdAsync(getIdRequest);
                
                // Get credentials
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
                // Log error but don't fail authentication
                try
                {
                    EventLog.WriteEntry("S3FileManager", $"Failed to get AWS credentials: {ex.Message}", EventLogEntryType.Error);
                }
                catch
                {
                    // Swallow any exceptions from logging to avoid affecting application flow
                }
            }
        }

        /// <summary>
        /// Get user details from Cognito
        /// </summary>
        private async Task<Dictionary<string, string>> GetUserDetailsAsync(string accessToken)
        {
            var request = new GetUserRequest
            {
                AccessToken = accessToken
            };
            
            var response = await _cognitoClient.GetUserAsync(request);
            
            return response.UserAttributes.ToDictionary(
                attr => attr.Name,
                attr => attr.Value);
        }

        /// <summary>
        /// Get user groups from Cognito
        /// </summary>
        private async Task<List<string>> GetUserGroupsAsync(string username, string accessToken)
        {
            try
            {
                var request = new AdminListGroupsForUserRequest
                {
                    Username = username,
                    UserPoolId = _config.UserPoolId
                };
                
                var response = await _cognitoClient.AdminListGroupsForUserAsync(request);
                
                return response.Groups.Select(g => g.GroupName).ToList();
            }
            catch
            {
                // If admin call fails, try to extract groups from token
                // This would require JWT parsing which is not shown here
                return new List<string>();
            }
        }

        /// <summary>
        /// Cache credentials for offline use
        /// </summary>
        private async Task CacheCredentialsAsync(CognitoUser user, string password)
        {
            try
            {
                var cachedCredentials = new CachedCredentials
                {
                    Username = user.Username,
                    EncryptedPassword = HashPassword(password),
                    EncryptedRefreshToken = EncryptData(user.RefreshToken ?? ""),
                    CachedRole = user.Role,
                    LastSuccessfulLogin = DateTime.UtcNow,
                    CacheExpiry = DateTime.UtcNow.AddDays(_config.OfflineCacheDurationDays)
                };
                
                string json = JsonConvert.SerializeObject(cachedCredentials);
                string encrypted = EncryptData(json);
                
                await File.WriteAllTextAsync(_cacheFilePath, encrypted);
            }
            catch
            {
                // Don't fail authentication if caching fails
            }
        }

        /// <summary>
        /// Load cached credentials
        /// </summary>
        private async Task<CachedCredentials?> LoadCachedCredentialsAsync()
        {
            try
            {
                if (!File.Exists(_cacheFilePath))
                {
                    return null;
                }
                
                string encrypted = await File.ReadAllTextAsync(_cacheFilePath);
                string json = DecryptData(encrypted);
                
                return JsonConvert.DeserializeObject<CachedCredentials>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Check if system is in offline mode
        /// </summary>
        private bool IsOfflineMode()
        {
            try
            {
                // Simple connectivity check - can be enhanced
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(2);
                    var result = client.GetAsync($"https://cognito-idp.{_config.Region}.amazonaws.com").Result;
                    return false;
                }
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        /// Calculate secret hash for Cognito authentication
        /// </summary>
        private string CalculateSecretHash(string username, string clientId, string clientSecret)
        {
            byte[] message = Encoding.UTF8.GetBytes(username + clientId);
            byte[] key = Encoding.UTF8.GetBytes(clientSecret);
            
            using (var hmac = new HMACSHA256(key))
            {
                byte[] hash = hmac.ComputeHash(message);
                return Convert.ToBase64String(hash);
            }
        }

        /// <summary>
        /// Hash password for offline storage
        /// </summary>
        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + "StrataS3Salt"));
                return Convert.ToBase64String(bytes);
            }
        }

        /// <summary>
        /// Encrypt data using Windows DPAPI
        /// </summary>
        private string EncryptData(string data)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            byte[] encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }

        /// <summary>
        /// Decrypt data using Windows DPAPI
        /// <summary>
        /// Encrypt data using Windows DPAPI
        /// </summary>
        private string EncryptData(string data)
        {
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(data);
                byte[] encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(encrypted);
            }
            catch (CryptographicException ex)
            {
                // Log the error and return a fallback value or rethrow
                Console.WriteLine($"Encryption failed: {ex.Message}");
                return string.Empty;
            }
            catch (PlatformNotSupportedException ex)
            {
                // Log the error and return a fallback value or rethrow
                Console.WriteLine($"Encryption not supported on this platform: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Decrypt data using Windows DPAPI
        /// </summary>
        private string DecryptData(string encryptedData)
        {
            try
            {
                byte[] encrypted = Convert.FromBase64String(encryptedData);
                byte[] decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch (CryptographicException ex)
            {
                // Log the error and return a fallback value or rethrow
                Console.WriteLine($"Decryption failed: {ex.Message}");
                return string.Empty;
            }
            catch (PlatformNotSupportedException ex)
            {
                // Log the error and return a fallback value or rethrow
                Console.WriteLine($"Decryption not supported on this platform: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Sign out current user
        /// </summary>
        public async Task SignOutAsync()
        {
            if (_currentUser != null && !string.IsNullOrEmpty(_currentUser.AccessToken))
            {
                try
                {
                    var signOutRequest = new GlobalSignOutRequest
                    {
                        AccessToken = _currentUser.AccessToken
                    };
                    
                    await _cognitoClient.GlobalSignOutAsync(signOutRequest);
                }
                catch
                {
                    // Ignore sign out errors
                }
            }
            
            _currentUser = null;
        }

        /// <summary>
        /// Get current authenticated user
        /// </summary>
        public CognitoUser? GetCurrentUser()
        {
            return _currentUser;
        }

        public void Dispose()
        {
            _cognitoClient?.Dispose();
        }
    }
}
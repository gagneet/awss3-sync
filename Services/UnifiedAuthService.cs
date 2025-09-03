using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using AWSS3Sync.Models;

namespace AWSS3Sync.Services
{
    /// <summary>
    /// Unified authentication service that consolidates Cognito and local authentication
    /// </summary>
    public class UnifiedAuthService : IDisposable
    {
        private readonly CognitoAuthService? _cognitoService;
        private readonly UserService _localUserService;
        private bool _cognitoAvailable;
        
        public UnifiedAuthService()
        {
            // Initialize local user service (always available)
            _localUserService = new UserService();
            
            // Try to initialize Cognito service
            try
            {
                _cognitoService = new CognitoAuthService();
                _cognitoAvailable = true;
            }
            catch (Exception ex)
            {
                _cognitoAvailable = false;
                System.Diagnostics.Debug.WriteLine($"Cognito service unavailable: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Authenticate user with automatic method selection and fallback
        /// </summary>
        public async Task<AuthenticationResult> AuthenticateAsync(string username, string password, 
            bool preferOffline = false, bool allowLocalFallback = true)
        {
            // Primary: Try Cognito authentication if available
            if (_cognitoAvailable && _cognitoService != null)
            {
                var cognitoResult = await TryCognitoAuthentication(username, password, preferOffline);
                if (cognitoResult.IsSuccess)
                {
                    return cognitoResult;
                }
                
                // If Cognito failed and fallback is allowed, try local authentication
                if (allowLocalFallback)
                {
                    var localResult = TryLocalAuthentication(username, password);
                    if (localResult.IsSuccess)
                    {
                        localResult.MethodUsed = AuthenticationMethod.Fallback;
                        localResult.WarningMessage = "Cognito authentication failed. Authenticated using local credentials with limited access.";
                        localResult.RequiresAwsCredentialWarning = true;
                        return localResult;
                    }
                }
                
                return cognitoResult; // Return original Cognito error
            }
            
            // Fallback: Try local authentication
            if (allowLocalFallback)
            {
                var localResult = TryLocalAuthentication(username, password);
                if (localResult.IsSuccess && !_cognitoAvailable)
                {
                    localResult.WarningMessage = "AWS Cognito is not available. Using local authentication with limited access.";
                    localResult.RequiresAwsCredentialWarning = true;
                }
                return localResult;
            }
            
            return AuthenticationResult.Failure(
                "No authentication methods are available.", 
                AuthenticationMethod.Unknown);
        }
        
        /// <summary>
        /// Try Cognito authentication with offline support
        /// </summary>
        private async Task<AuthenticationResult> TryCognitoAuthentication(string username, string password, bool preferOffline)
        {
            try
            {
                if (_cognitoService == null)
                    return AuthenticationResult.Failure("Cognito service not available", AuthenticationMethod.CognitoOnline);
                
                var cognitoUser = await _cognitoService.AuthenticateAsync(username, password, !preferOffline);
                
                if (cognitoUser != null)
                {
                    var unifiedUser = UnifiedUser.FromCognitoUser(cognitoUser);
                    
                    if (cognitoUser.IsOfflineMode)
                    {
                        return AuthenticationResult.OfflineSuccess(unifiedUser);
                    }
                    else
                    {
                        return AuthenticationResult.Success(unifiedUser, AuthenticationMethod.CognitoOnline);
                    }
                }
                
                return AuthenticationResult.Failure("Invalid credentials", AuthenticationMethod.CognitoOnline);
            }
            catch (Exception ex)
            {
                return AuthenticationResult.Failure($"Cognito authentication failed: {ex.Message}", AuthenticationMethod.CognitoOnline);
            }
        }
        
        /// <summary>
        /// Try local authentication
        /// </summary>
        private AuthenticationResult TryLocalAuthentication(string username, string password)
        {
            try
            {
                var localUser = _localUserService.ValidateUser(username, password);
                
                if (localUser != null)
                {
                    var unifiedUser = UnifiedUser.FromLocalUser(localUser);
                    return AuthenticationResult.Success(unifiedUser, AuthenticationMethod.Local);
                }
                
                return AuthenticationResult.Failure("Invalid local credentials", AuthenticationMethod.Local);
            }
            catch (Exception ex)
            {
                return AuthenticationResult.Failure($"Local authentication failed: {ex.Message}", AuthenticationMethod.Local);
            }
        }
        
        /// <summary>
        /// Check if Cognito authentication is available
        /// </summary>
        public bool IsCognitoAvailable => _cognitoAvailable && _cognitoService != null;
        
        /// <summary>
        /// Check if local authentication is available
        /// </summary>
        public bool IsLocalAuthAvailable => _localUserService != null;
        
        /// <summary>
        /// Get authentication status description
        /// </summary>
        public string GetAuthenticationStatus()
        {
            if (IsCognitoAvailable && IsLocalAuthAvailable)
                return "AWS Cognito and Local authentication available";
            else if (IsCognitoAvailable)
                return "AWS Cognito authentication available";
            else if (IsLocalAuthAvailable)
                return "Local authentication only (Limited access)";
            else
                return "No authentication methods available";
        }
        
        /// <summary>
        /// Validate that a user has AWS credentials for S3 operations
        /// </summary>
        public static bool ValidateAwsCredentials(UnifiedUser user)
        {
            return user.HasAwsCredentials;
        }
        
        /// <summary>
        /// Get warning message for limited access users
        /// </summary>
        public static string GetLimitedAccessWarning(UnifiedUser user)
        {
            if (user.AuthType == AuthenticationType.Local && !user.HasAwsCredentials)
            {
                return $"Warning: User '{user.Username}' does not have AWS credentials. " +
                       "S3 operations will fail. Please configure AWS Cognito authentication for full access.";
            }
            
            return string.Empty;
        }
        
        public void Dispose()
        {
            _cognitoService?.Dispose();
        }
    }
}
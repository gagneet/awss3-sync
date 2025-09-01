using System;

namespace S3FileManager.Models
{
    /// <summary>
    /// Result of unified authentication attempt
    /// </summary>
    public class AuthenticationResult
    {
        public bool IsSuccess { get; set; }
        public UnifiedUser? User { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public AuthenticationMethod MethodUsed { get; set; }
        public bool RequiresAwsCredentialWarning { get; set; }
        public string? WarningMessage { get; set; }
        
        /// <summary>
        /// Create successful authentication result
        /// </summary>
        public static AuthenticationResult Success(UnifiedUser user, AuthenticationMethod method)
        {
            var result = new AuthenticationResult
            {
                IsSuccess = true,
                User = user,
                MethodUsed = method
            };
            
            // Check if we need to warn about limited access
            if (user.IsLimitedAccess)
            {
                result.RequiresAwsCredentialWarning = true;
                result.WarningMessage = $"You are authenticated as '{user.Username}' but do not have AWS credentials. " +
                    "You will have limited access to S3 operations. Please contact your administrator " +
                    "to set up AWS Cognito authentication for full access.";
            }
            
            return result;
        }
        
        /// <summary>
        /// Create failed authentication result
        /// </summary>
        public static AuthenticationResult Failure(string errorMessage, AuthenticationMethod attemptedMethod = AuthenticationMethod.Unknown)
        {
            return new AuthenticationResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage,
                MethodUsed = attemptedMethod
            };
        }
        
        /// <summary>
        /// Create result for offline authentication
        /// </summary>
        public static AuthenticationResult OfflineSuccess(UnifiedUser user)
        {
            return new AuthenticationResult
            {
                IsSuccess = true,
                User = user,
                MethodUsed = AuthenticationMethod.CognitoOffline,
                WarningMessage = "You are authenticated using cached credentials. Some features may be limited in offline mode."
            };
        }
    }
    
    /// <summary>
    /// Methods of authentication attempted
    /// </summary>
    public enum AuthenticationMethod
    {
        Unknown,
        CognitoOnline,
        CognitoOffline,
        Local,
        Fallback
    }
}
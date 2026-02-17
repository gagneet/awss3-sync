using System;
using System.Collections.Generic;

namespace AWSS3Sync.Models
{
    /// <summary>
    /// Unified user model that abstracts both Cognito and local authentication
    /// </summary>
    public class UnifiedUser
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public DateTime LastLogin { get; set; }
        public AuthenticationType AuthType { get; set; }
        
        // Cognito-specific properties
        public string? CognitoSub { get; set; }
        public List<string> Groups { get; set; } = new List<string>();
        public DateTime? TokenExpiry { get; set; }
        
        // AWS Credentials (from Cognito or mapped)
        public string? AwsAccessKeyId { get; set; }
        public string? AwsSecretAccessKey { get; set; }
        public string? AwsSessionToken { get; set; }
        
        // Authentication tokens (Cognito only)
        public string? AccessToken { get; set; }
        public string? IdToken { get; set; }
        public string? RefreshToken { get; set; }
        
        // Status flags
        public bool IsOfflineMode { get; set; }
        public bool HasAwsCredentials => !string.IsNullOrEmpty(AwsAccessKeyId) && !string.IsNullOrEmpty(AwsSecretAccessKey);
        public bool IsLimitedAccess => AuthType == AuthenticationType.Local && !HasAwsCredentials;
        
        /// <summary>
        /// Check if the user has valid credentials for their authentication type
        /// </summary>
        public bool HasValidCredentials()
        {
            switch (AuthType)
            {
                case AuthenticationType.Cognito:
                    if (IsOfflineMode)
                    {
                        return !string.IsNullOrEmpty(Username);
                    }
                    return !string.IsNullOrEmpty(AccessToken) && (TokenExpiry == null || TokenExpiry > DateTime.UtcNow);
                    
                case AuthenticationType.Local:
                    return !string.IsNullOrEmpty(Username);
                    
                default:
                    return false;
            }
        }
        
        /// <summary>
        /// Create UnifiedUser from CognitoUser
        /// </summary>
        public static UnifiedUser FromCognitoUser(CognitoUser cognitoUser)
        {
            return new UnifiedUser
            {
                Username = cognitoUser.Username,
                Email = cognitoUser.Email,
                Role = cognitoUser.Role,
                LastLogin = cognitoUser.LastLogin,
                AuthType = AuthenticationType.Cognito,
                CognitoSub = cognitoUser.Sub,
                Groups = cognitoUser.Groups,
                TokenExpiry = cognitoUser.TokenExpiry,
                AwsAccessKeyId = cognitoUser.AwsAccessKeyId,
                AwsSecretAccessKey = cognitoUser.AwsSecretAccessKey,
                AwsSessionToken = cognitoUser.AwsSessionToken,
                AccessToken = cognitoUser.AccessToken,
                IdToken = cognitoUser.IdToken,
                RefreshToken = cognitoUser.RefreshToken,
                IsOfflineMode = cognitoUser.IsOfflineMode
            };
        }
        
        /// <summary>
        /// Create UnifiedUser from local User
        /// </summary>
        public static UnifiedUser FromLocalUser(User localUser)
        {
            return new UnifiedUser
            {
                Username = localUser.Username,
                Role = localUser.Role,
                LastLogin = localUser.LastLogin,
                AuthType = AuthenticationType.Local,
                Email = string.Empty, // Local users don't have email
                IsOfflineMode = false
            };
        }
        
        /// <summary>
        /// Get capability description for user
        /// </summary>
        public string GetCapabilityDescription()
        {
            if (AuthType == AuthenticationType.Cognito && HasAwsCredentials)
            {
                return IsOfflineMode ? "Full access (Offline Mode)" : "Full access (AWS Authenticated)";
            }
            else if (AuthType == AuthenticationType.Local && !HasAwsCredentials)
            {
                return "Limited access (No AWS credentials)";
            }
            else if (AuthType == AuthenticationType.Local && HasAwsCredentials)
            {
                return "Full access (Mapped AWS credentials)";
            }
            else
            {
                return "Unknown access level";
            }
        }
    }
    
    /// <summary>
    /// Authentication type enumeration
    /// </summary>
    public enum AuthenticationType
    {
        Cognito,
        Local
    }
}
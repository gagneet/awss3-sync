using System;
using System.Collections.Generic;
using AWSS3Sync.Models;

namespace AWSS3Sync.Models
{
    /// <summary>
    /// Represents a user authenticated through AWS Cognito with offline capabilities
    /// </summary>
    public class CognitoUser
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Sub { get; set; } = string.Empty; // Cognito User ID
        public UserRole Role { get; set; }
        public List<string> Groups { get; set; } = new List<string>();
        public DateTime LastLogin { get; set; }
        public DateTime TokenExpiry { get; set; }
        
        // Cached credentials for offline access
        public CachedCredentials? CachedCredentials { get; set; }
        
        // Temporary AWS credentials from Cognito
        public string? AccessToken { get; set; }
        public string? IdToken { get; set; }
        public string? RefreshToken { get; set; }
        
        // AWS temporary credentials for S3 access
        public string? AwsAccessKeyId { get; set; }
        public string? AwsSecretAccessKey { get; set; }
        public string? AwsSessionToken { get; set; }
        
        public bool IsOfflineMode { get; set; }
        
        /// <summary>
        /// Check if the user has valid credentials (online or cached)
        /// </summary>
        public bool HasValidCredentials()
        {
            if (IsOfflineMode)
            {
                return CachedCredentials != null && CachedCredentials.IsValid();
            }
            
            return !string.IsNullOrEmpty(AccessToken) && TokenExpiry > DateTime.UtcNow;
        }
        
        /// <summary>
        /// Map Cognito groups to application roles
        /// </summary>
        public static UserRole MapGroupsToRole(List<string> groups)
        {
            if (groups == null || groups.Count == 0)
                return UserRole.User;
            
            // Priority order: Administrator > Executive > User
            if (groups.Contains("strata-admin") || groups.Contains("Administrator"))
                return UserRole.Administrator;
            
            if (groups.Contains("strata-ec") || groups.Contains("Executive") || groups.Contains("ExecutiveCommittee"))
                return UserRole.Executive;
            
            return UserRole.User; // Default role for residents
        }
    }
    
    /// <summary>
    /// Cached credentials for offline access
    /// </summary>
    public class CachedCredentials
    {
        public string Username { get; set; } = string.Empty;
        public string EncryptedPassword { get; set; } = string.Empty;
        public string EncryptedRefreshToken { get; set; } = string.Empty;
        public UserRole CachedRole { get; set; }
        public DateTime LastSuccessfulLogin { get; set; }
        public DateTime CacheExpiry { get; set; }
        
        /// <summary>
        /// Check if cached credentials are still valid
        /// </summary>
        public bool IsValid()
        {
            return CacheExpiry > DateTime.UtcNow && !string.IsNullOrEmpty(EncryptedRefreshToken);
        }
    }
    
    /// <summary>
    /// Cognito configuration settings
    /// </summary>
    public class CognitoConfig
    {
        public string UserPoolId { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public string IdentityPoolId { get; set; } = string.Empty;
        public bool EnableOfflineMode { get; set; } = true;
        public int OfflineCacheDurationDays { get; set; } = 7;
    }
}
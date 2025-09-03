# AWS IAM and Cognito Setup Guide for Strata S3 Manager

## Overview
This guide will help you set up AWS IAM and Cognito for your Strata S3 Manager application, enabling role-based access control with offline capabilities.

**New in v2.0:** The application now features a **Unified Authentication System** that provides:
- Automatic fallback between Cognito and local authentication
- Enhanced security warnings for users without AWS credentials
- Improved user experience with single login interface
- Better error handling and user guidance

## Security Enhancements

### Critical Security Fix
Previous versions allowed local users to authenticate without AWS credentials, creating security vulnerabilities:
- Local users could not actually perform S3 operations (missing AWS credentials)
- Users bypassed the carefully configured AWS IAM security system
- Inconsistent role enforcement between authentication methods

### New Security Model
The Unified Authentication System addresses these issues:
- **Primary Authentication:** AWS Cognito with full IAM integration
- **Fallback Authentication:** Local authentication with clear warnings about limited access
- **Credential Validation:** All S3 operations validate AWS credentials before execution
- **User Guidance:** Clear warnings when users lack proper AWS access

## Table of Contents
1. [Unified Authentication System](#unified-authentication-system)
2. [AWS Cognito Setup](#aws-cognito-setup)
3. [IAM Roles Configuration](#iam-roles-configuration)
4. [Application Configuration](#application-configuration)
5. [Performance Optimization](#performance-optimization)
6. [Migration Guide](#migration-guide)
7. [Troubleshooting](#troubleshooting)

## Unified Authentication System

### Authentication Flow
1. **Primary:** Application attempts AWS Cognito authentication
2. **Fallback:** If Cognito fails or is unavailable, falls back to local authentication
3. **Security:** Users without AWS credentials receive prominent warnings
4. **Validation:** All S3 operations validate credentials before execution

### User Experience
- Single login form with intelligent method detection
- Clear status indicators for authentication state
- Progressive disclosure of authentication options
- Comprehensive help and troubleshooting guidance

### For Administrators
- Enhanced security with proper credential validation
- Clear warnings help users understand access limitations
- Gradual migration path from local to Cognito authentication
- Better error reporting for troubleshooting authentication issues

## AWS Cognito Setup

### Step 1: Create a Cognito User Pool

1. Navigate to AWS Cognito Console
2. Click "Create user pool"
3. Configure sign-in options:
   - Select "Email" and "Username" as sign-in options
   - Enable case-insensitive username

4. Configure security requirements:
   - Password policy: 
     - Minimum length: 8
     - Require numbers, special characters, uppercase and lowercase
   - MFA: Optional (recommended for admin users)
   - Account recovery: Email only

5. Configure sign-up experience:
   - Enable self-registration: No (admin-managed users)
   - Required attributes: email, name
   - Custom attributes: Add "role" (string)

6. Configure message delivery:
   - Email provider: Cognito default
   - FROM email address: your-strata@example.com

7. Create app client:
   - App client name: "StrataS3Manager"
   - Authentication flows:
     - ✅ ALLOW_USER_PASSWORD_AUTH
     - ✅ ALLOW_REFRESH_TOKEN_AUTH
   - Generate client secret: Yes (for enhanced security)

8. Review and create the user pool

### Step 2: Create User Groups

Create three groups in your user pool:

1. **strata-admin** (Administrators)
   - Description: Full access to all S3 files and management features
   - IAM role: Will be created later

2. **strata-ec** (Executive Committee)
   - Description: Access to executive documents and resident files
   - IAM role: Will be created later

3. **strata-residents** (Residents)
   - Description: Access to public documents only
   - IAM role: Will be created later

### Step 3: Create Cognito Identity Pool

1. Navigate to Cognito Identity Pools
2. Create new identity pool:
   - Identity pool name: "StrataS3ManagerIdentityPool"
   - Authentication providers:
     - Cognito: Select your user pool
     - User Pool ID: [Your User Pool ID]
     - App client ID: [Your App Client ID]

3. Configure permissions:
   - Create new IAM roles for authenticated and unauthenticated users

## IAM Roles Configuration

### Step 1: Create IAM Policies

Create three policies for each user group:

#### Administrator Policy
```json
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Effect": "Allow",
            "Action": [
                "s3:ListBucket",
                "s3:GetBucketLocation"
            ],
            "Resource": "arn:aws:s3:::your-strata-bucket"
        },
        {
            "Effect": "Allow",
            "Action": [
                "s3:GetObject",
                "s3:PutObject",
                "s3:DeleteObject",
                "s3:GetObjectVersion",
                "s3:PutObjectAcl"
            ],
            "Resource": "arn:aws:s3:::your-strata-bucket/*"
        }
    ]
}
```

#### Executive Committee Policy
```json
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Effect": "Allow",
            "Action": [
                "s3:ListBucket",
                "s3:GetBucketLocation"
            ],
            "Resource": "arn:aws:s3:::your-strata-bucket",
            "Condition": {
                "StringLike": {
                    "s3:prefix": [
                        "public/*",
                        "executive-committee/*",
                        "meeting-minutes/*"
                    ]
                }
            }
        },
        {
            "Effect": "Allow",
            "Action": [
                "s3:GetObject",
                "s3:PutObject"
            ],
            "Resource": [
                "arn:aws:s3:::your-strata-bucket/public/*",
                "arn:aws:s3:::your-strata-bucket/executive-committee/*",
                "arn:aws:s3:::your-strata-bucket/meeting-minutes/*"
            ]
        }
    ]
}
```

#### Resident Policy
```json
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Effect": "Allow",
            "Action": [
                "s3:ListBucket",
                "s3:GetBucketLocation"
            ],
            "Resource": "arn:aws:s3:::your-strata-bucket",
            "Condition": {
                "StringLike": {
                    "s3:prefix": [
                        "public/*",
                        "notices/*"
                    ]
                }
            }
        },
        {
            "Effect": "Allow",
            "Action": "s3:GetObject",
            "Resource": [
                "arn:aws:s3:::your-strata-bucket/public/*",
                "arn:aws:s3:::your-strata-bucket/notices/*"
            ]
        }
    ]
}
```

### Step 2: Create IAM Roles

1. Create three IAM roles:
   - StrataAdminRole
   - StrataECRole
   - StrataResidentRole

2. Attach the corresponding policies to each role

3. Configure trust relationships for Cognito:
```json
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Effect": "Allow",
            "Principal": {
                "Federated": "cognito-identity.amazonaws.com"
            },
            "Action": "sts:AssumeRoleWithWebIdentity",
            "Condition": {
                "StringEquals": {
                    "cognito-identity.amazonaws.com:aud": "YOUR_IDENTITY_POOL_ID"
                },
                "ForAnyValue:StringLike": {
                    "cognito-identity.amazonaws.com:amr": "authenticated"
                }
            }
        }
    ]
}
```

## Application Configuration

### Update appsettings.json

```json
{
  "AWS": {
    "AccessKey": "FALLBACK_ACCESS_KEY",
    "SecretKey": "FALLBACK_SECRET_KEY",
    "Region": "ap-southeast-2",
    "BucketName": "your-strata-bucket"
  },
  "Cognito": {
    "UserPoolId": "ap-southeast-2_XXXXXXXXX",
    "ClientId": "your-app-client-id",
    "ClientSecret": "your-client-secret",
    "Region": "ap-southeast-2",
    "IdentityPoolId": "ap-southeast-2:xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "EnableOfflineMode": true,
    "OfflineCacheDurationDays": 7
  },
  "Performance": {
    "MaxConcurrentUploads": 5,
    "MaxConcurrentDownloads": 5,
    "ChunkSizeBytes": 5242880,
    "EnableMetadataCache": true,
    "MetadataCacheDurationMinutes": 5,
    "EnableDeltaSync": true,
    "SyncBatchSize": 100
  }
}
```

## Performance Optimization

### Implemented Improvements

1. **Parallel Operations**
   - Concurrent uploads/downloads (configurable limit)
   - Batch processing for sync operations
   - Channel-based producer-consumer pattern

2. **Chunked Transfers**
   - Large files split into 5MB chunks
   - Multipart upload for files > 5MB
   - Resume capability for interrupted transfers

3. **Metadata Caching**
   - 5-minute cache for S3 object metadata
   - Reduces API calls for frequently accessed files
   - Automatic cache cleanup

4. **Delta Sync**
   - Only syncs changed files (timestamp and size comparison)
   - Identifies extra local files
   - Batch processing for large directories

5. **Offline Mode**
   - Cached credentials for 7 days
   - Encrypted storage using Windows DPAPI
   - Automatic fallback when network unavailable

### S3 Bucket Organization

Recommended folder structure:
```
your-strata-bucket/
├── public/              # Accessible to all residents
│   ├── notices/
│   ├── newsletters/
│   └── general-info/
├── executive-committee/ # EC and Admin only
│   ├── meeting-minutes/
│   ├── financial-reports/
│   └── planning/
├── admin/              # Admin only
│   ├── contracts/
│   ├── legal/
│   └── sensitive/
└── logs/              # System logs (filtered from view)
```

## Creating Users

### Via AWS Console

1. Navigate to your Cognito User Pool
2. Click "Users" → "Create user"
3. Set username and temporary password
4. Add user to appropriate group:
   - strata-admin
   - strata-ec
   - strata-residents

### Via AWS CLI

```bash
# Create user
aws cognito-idp admin-create-user \
  --user-pool-id YOUR_POOL_ID \
  --username john.doe \
  --user-attributes Name=email,Value=john.doe@example.com \
  --temporary-password TempPass123!

# Add to group
aws cognito-idp admin-add-user-to-group \
  --user-pool-id YOUR_POOL_ID \
  --username john.doe \
  --group-name strata-residents
```

## Troubleshooting

### Common Issues and Solutions

1. **"Authentication failed" error**
   - Verify Cognito User Pool ID and Client ID
   - Check user exists and password is correct
   - Ensure user is confirmed (not in FORCE_CHANGE_PASSWORD state)

2. **"Access Denied" when accessing S3**
   - Verify IAM policies are correctly attached
   - Check S3 bucket name in configuration
   - Ensure Cognito Identity Pool is properly configured

3. **Slow sync performance**
   - Increase MaxConcurrentDownloads in settings
   - Check network bandwidth
   - Enable metadata caching if disabled

4. **Offline mode not working**
   - Ensure EnableOfflineMode is true in config
   - Check if credentials were cached (login online first)
   - Verify Windows user has permission to use DPAPI

5. **Token expiration issues**
   - Application automatically refreshes tokens every 30 minutes
   - If issues persist, logout and login again

### Logging

Enable detailed logging by adding to appsettings.json:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "AWSS3Sync": "Debug",
      "Amazon": "Warning"
    }
  }
}
```

## Security Best Practices

1. **Never commit credentials to source control**
   - Use environment variables or secure configuration
   - Rotate access keys regularly

2. **Enable MFA for administrator accounts**
   - Configure in Cognito User Pool settings
   - Require for sensitive operations

3. **Regular security audits**
   - Review IAM policies quarterly
   - Monitor CloudTrail logs for unusual activity
   - Update user group memberships as needed

4. **Secure offline cache**
   - Cached credentials encrypted with Windows DPAPI
   - Cache expires after 7 days
   - Clear cache on shared computers

## Migration Guide

### Migrating from Local-Only Authentication

If you're currently using local authentication and want to enable full AWS integration:

#### Step 1: Set Up AWS Cognito (if not already done)
1. Follow the AWS Cognito Setup section above
2. Create User Pool and configure groups
3. Set up Identity Pool for temporary credentials
4. Test Cognito authentication with a test user

#### Step 2: Migrate Users to Cognito
1. **Create Cognito Users:**
   - Add existing local users to Cognito User Pool
   - Use same usernames where possible for continuity
   - Assign users to appropriate groups (strata-admin, strata-ec, residents)

2. **Communication to Users:**
   - Inform users about enhanced security and new features
   - Provide temporary passwords for Cognito accounts
   - Explain that local authentication remains available as fallback

3. **Gradual Migration:**
   - Users can continue using local authentication during transition
   - Unified login form automatically detects and recommends Cognito
   - Security warnings guide users toward proper authentication

#### Step 3: Test and Validate
1. **Test Authentication Methods:**
   - Verify Cognito users can log in and access appropriate S3 resources
   - Confirm local users receive security warnings about limited access
   - Test automatic fallback when Cognito is unavailable

2. **Validate S3 Permissions:**
   - Test file operations with Cognito users
   - Verify local users receive appropriate error messages for S3 operations
   - Check role-based access controls are working properly

#### Step 4: Monitor and Support
1. **User Support:**
   - Provide clear documentation about new authentication system
   - Help users understand difference between full and limited access
   - Assist with Cognito password setup and group assignments

2. **Security Monitoring:**
   - Monitor authentication patterns and failures
   - Review AWS CloudTrail logs for access patterns
   - Update user group memberships as organizational roles change

### Rollback Plan

If issues occur during migration:

1. **Application Level:**
   - Users can still authenticate via local method
   - No functionality is lost for properly configured users
   - Enhanced error messages help identify issues

2. **AWS Configuration:**
   - Cognito User Pool can be disabled without affecting local auth
   - IAM policies can be adjusted without breaking existing access
   - Identity Pool can be reconfigured if credential issues arise

3. **Emergency Access:**
   - Administrator can temporarily enable broader local access if needed
   - Configuration can be rolled back to previous authentication model
   - Local user database remains intact during migration

## Support

For additional help:
1. Check AWS CloudWatch logs for detailed error messages
2. Review Cognito User Pool audit logs
3. Check the new `UNIFIED_AUTHENTICATION_GUIDE.md` for detailed technical documentation
4. Contact your AWS administrator
5. Submit issues to the project repository
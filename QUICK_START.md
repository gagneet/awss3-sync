# Quick Start Guide - Strata S3 Manager

## Prerequisites

1. **.NET 8.0 SDK** - [Download here](https://dotnet.microsoft.com/download/dotnet/8.0)
2. **AWS Account** with appropriate permissions
3. **Windows 10/11** (for Windows Forms application)

## Step 1: Build the Application

Open PowerShell and run:

```powershell
# Clone or navigate to your project directory
cd path\to\AWSS3Sync

# Run the build script
.\build.ps1 -Configuration Release

# Or build with single file output
.\build.ps1 -Configuration Release -SingleFile
```

## Step 2: Configure AWS Resources

### Quick Setup (Minimal Configuration)

1. **Create S3 Bucket**:
   ```bash
   aws s3 mb s3://your-strata-bucket --region ap-southeast-2
   ```

2. **Create Cognito User Pool** (AWS Console):
   - Go to AWS Cognito Console
   - Create User Pool with email/username sign-in
   - Create app client with USER_PASSWORD_AUTH flow
   - Note down: User Pool ID, Client ID

3. **Create test users**:
   ```bash
   # Admin user
   aws cognito-idp admin-create-user \
     --user-pool-id YOUR_POOL_ID \
     --username admin@strata.com \
     --user-attributes Name=email,Value=admin@strata.com \
     --temporary-password Admin123!

   # Add to admin group
   aws cognito-idp admin-add-user-to-group \
     --user-pool-id YOUR_POOL_ID \
     --username admin@strata.com \
     --group-name strata-admin
   ```

## Step 3: Configure Application

Edit `publish\appsettings.json`:

```json
{
  "AWS": {
    "AccessKey": "AKIA...",  // Your AWS access key
    "SecretKey": "...",       // Your AWS secret key
    "Region": "ap-southeast-2",
    "BucketName": "your-strata-bucket"
  },
  "Cognito": {
    "UserPoolId": "ap-southeast-2_XXXXX",
    "ClientId": "your-client-id",
    "ClientSecret": "",  // Optional
    "Region": "ap-southeast-2",
    "IdentityPoolId": "",  // Optional for now
    "EnableOfflineMode": true,
    "OfflineCacheDurationDays": 7
  }
}
```

## Step 4: Run the Application

```powershell
cd publish
.\AWSS3Sync.exe
```

## Step 5: Login and Use

### First Time Login (Cognito Mode)

1. Select **"AWS Cognito (Recommended)"** mode
2. Enter your Cognito username/email
3. Enter your password
4. Click **Login**

### Using the Application

#### Upload Files:
1. Click **Browse** to select local folder
2. Check files/folders to upload
3. Click **Upload** button
4. Monitor progress in status bar

#### Download Files:
1. Check S3 files to download
2. Click **Download** button
3. Select destination folder
4. Monitor progress

#### Sync Folder:
1. Click **Sync** button
2. Select local folder to sync with S3
3. Review sync report
4. Optionally delete extra local files

#### Offline Mode:
- Check **"Offline Mode"** at login if no internet
- Uses cached credentials from last online login
- Limited to 7 days by default

## Performance Tips

### For Large File Sets (1000+ files)

1. **Increase concurrent operations** in `appsettings.json`:
   ```json
   "Performance": {
     "MaxConcurrentUploads": 10,
     "MaxConcurrentDownloads": 10
   }
   ```

2. **Enable metadata caching**:
   ```json
   "EnableMetadataCache": true,
   "MetadataCacheDurationMinutes": 10
   ```

3. **Use delta sync** for regular syncs:
   ```json
   "EnableDeltaSync": true,
   "SyncBatchSize": 200
   ```

## Troubleshooting

### Common Issues

#### "Authentication failed"
- Verify Cognito User Pool ID and Client ID
- Check username and password
- Ensure user account is confirmed

#### "Access Denied" on S3
- Check AWS credentials in appsettings.json
- Verify S3 bucket name
- Ensure IAM user has S3 permissions

#### Slow Performance
- Increase MaxConcurrent settings
- Check network bandwidth
- Enable metadata caching

#### Offline Mode Not Working
- Login online at least once first
- Check EnableOfflineMode is true
- Verify cached credentials haven't expired

## Testing Different Roles

### Create Test Users

```powershell
# Executive Committee user
aws cognito-idp admin-create-user `
  --user-pool-id YOUR_POOL_ID `
  --username ec@strata.com `
  --temporary-password EC123!

aws cognito-idp admin-add-user-to-group `
  --user-pool-id YOUR_POOL_ID `
  --username ec@strata.com `
  --group-name strata-ec

# Resident user
aws cognito-idp admin-create-user `
  --user-pool-id YOUR_POOL_ID `
  --username resident@strata.com `
  --temporary-password Resident123!

aws cognito-idp admin-add-user-to-group `
  --user-pool-id YOUR_POOL_ID `
  --username resident@strata.com `
  --group-name strata-residents
```

### Test Folder Structure

Create this structure in your S3 bucket:

```
your-strata-bucket/
├── public/
│   ├── notices/
│   │   └── notice.pdf
│   └── newsletters/
│       └── newsletter.pdf
├── executive-committee/
│   ├── meeting-minutes/
│   │   └── minutes.docx
│   └── budget/
│       └── budget.xlsx
└── admin/
    ├── contracts/
    │   └── contract.pdf
    └── sensitive/
        └── confidential.pdf
```

Test access:
- **Admin**: Can see all folders
- **EC**: Can see public/ and executive-committee/
- **Resident**: Can only see public/

## Legacy Mode (Fallback)

If Cognito is not configured:

1. Select **"Legacy Mode"** at login
2. Enter any username
3. Select role manually
4. Click Login

Note: Legacy mode doesn't provide AWS IAM security.

## Next Steps

For production deployment:

1. **Follow full AWS setup**: See `AWS_IAM_SETUP_GUIDE.md`
2. **Configure IAM roles**: For proper role-based access
3. **Set up Identity Pool**: For temporary AWS credentials
4. **Enable MFA**: For administrator accounts
5. **Configure backup**: Regular S3 versioning/backup

## Support

- Check `IMPROVEMENTS_SUMMARY.md` for feature details
- Review `AWS_IAM_SETUP_GUIDE.md` for complete setup
- Check application logs in `%LOCALAPPDATA%\StrataS3Manager\`

## Quick Command Reference

```powershell
# Build
.\build.ps1

# Run
.\publish\AWSS3Sync.exe

# Create user (AWS CLI)
aws cognito-idp admin-create-user --user-pool-id YOUR_POOL_ID --username user@example.com

# Add to group
aws cognito-idp admin-add-user-to-group --user-pool-id YOUR_POOL_ID --username user@example.com --group-name strata-admin

# List users
aws cognito-idp list-users --user-pool-id YOUR_POOL_ID

# List groups
aws cognito-idp list-groups --user-pool-id YOUR_POOL_ID
```
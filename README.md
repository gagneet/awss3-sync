# AWS S3 Sync Utility

A comprehensive Windows desktop application for managing AWS S3 bucket files with **unified authentication**, role-based access control, hierarchical file navigation, and advanced permission management.

![Application Screenshot](https://img.shields.io/badge/Platform-Windows-blue) ![.NET](https://img.shields.io/badge/.NET-8.0-purple) ![AWS](https://img.shields.io/badge/AWS-S3-orange) ![Security](https://img.shields.io/badge/Security-Enhanced-green)

## 🆕 What's New in v2.0

### 🔐 **Unified Authentication System**
- **Single Login Interface**: Intelligent authentication with automatic method detection
- **Enhanced Security**: Proper AWS credential validation prevents S3 operation failures
- **Automatic Fallback**: Seamlessly switches from Cognito to local authentication when needed
- **Security Warnings**: Clear guidance when users have limited access
- **Improved UX**: Progressive disclosure and comprehensive help system

### 🛡️ **Critical Security Fixes**
- **Credential Validation**: All S3 operations now validate AWS credentials before execution
- **Access Warnings**: Users without AWS credentials receive prominent warnings
- **Secure Architecture**: Local users can no longer bypass AWS IAM security controls
- **Error Prevention**: Prevents runtime S3 failures due to missing credentials

## 📚 Documentation

- [**Unified Authentication Guide**](UNIFIED_AUTHENTICATION_GUIDE.md): **NEW** - Complete documentation of the unified authentication system
- [User Guide](User_Guide.md): Step-by-step instructions for installation, configuration, and daily use
- [AWS IAM Setup Guide](AWS_IAM_SETUP_GUIDE.md): **UPDATED** - Enhanced with migration guidance and security improvements
- [Developer Guide](Developer_Guide.md): Architecture overview, coding standards, contribution workflow, and known gaps/TODOs
- [Sync Feature Test Plan](SyncFeatureTestPlan.md): Checklist and template for validating the sync functionality

> **Migration Guide**: Existing users can continue using local authentication, but AWS Cognito is recommended for full functionality and security.

## 🌟 Features

### 🔐 **Enhanced Authentication & Security**
- **Unified Authentication**: Single login form with automatic Cognito/Local detection
- **AWS Cognito Integration**: Full IAM integration with role-based permissions
- **Offline Support**: Cached credentials for offline access
- **Security Validation**: All S3 operations validate credentials before execution
- **Clear Warnings**: Users understand their access level and limitations

### 🎭 **Role-Based Access Control**
- **Administrator**: Full access to all files, upload/download/delete capabilities, permission management
- **Executive**: Can download files, upload to specific folders, access executive-only content
- **User**: View-only access to specifically assigned files and folders

### 🌲 Explorer-Like Interface
- **Hierarchical Tree View**: Navigate local and S3 files and folders in a familiar parent-child structure.
- **Lazy Loading for Local Files**: Efficiently browse large local directories without long initial load times.
- **File Previewer**: Click on a file to see a preview of its content directly in the application. Supports common text and image formats.
- **Visual Icons**: Clear distinction between folders and files.
- **Checkbox Selection**: Multi-select files and folders for batch operations.

### 📁 File Operations
- **Upload**: Individual files or entire folder structures.
- **Download**: Selective downloading of files and folders.
- **Sync**: Synchronize local folders with an S3 bucket.  
  **Note:** This feature is still in development. See [SyncFeatureTestPlan.md](SyncFeatureTestPlan.md) for test details and limitations.
- **Delete**: Enhanced confirmation system for permanent deletion.

### 🛡️ Advanced Permission Management
- **Granular Control**: Set permissions per file or folder.
- **Recursive Permissions**: Apply folder permissions to all contents.
- **Dynamic Access**: Real-time permission changes.
- **Executive Upload Zones**: Predefined folders for Executive uploads.
- **Auto-Tagging System**: Automatically assigns "Permission: pending" to files missing proper tags.
- **Admin Review Workflow**: Dedicated interface for administrators to review and set permissions for auto-tagged files.
- **First-Run Setup**: Streamlined process to ensure all files have proper permission metadata.

## 🏗️ Project Structure
```
AWSS3Sync/
├── Models/
│   ├── UserRole.cs          # User roles and User class
│   ├── FileItem.cs          # Contains FileNode, LocalFileItem, S3FileItem models
│   └── AppConfig.cs         # Configuration classes
├── Services/
│   ├── ConfigurationService.cs  # Config loading and validation
│   ├── S3Service.cs             # S3 operations with role filtering
│   ├── MetadataService.cs       # Permission management
│   └── FileService.cs           # Local file utilities
├── Forms/
│   ├── LoginForm.cs             # User authentication
│   ├── MainForm.cs              # Main application UI and partial classes
│   ├── RoleSelectionForm.cs     # Permission assignment
│   ├── ProgressForm.cs          # Progress display
│   └── ... (other supporting forms)
└── Program.cs               # Application entry point
```

> For detailed architecture, coding guidelines, and contribution process, see [DEVELOPER_GUIDE.md](DEVELOPER_GUIDE.md).

## 🚀 Getting Started

### Prerequisites
- Windows 10/11
- .NET 8.0 SDK or later
- Visual Studio 2022 (for development)
- An active AWS Account with an S3 bucket and IAM user credentials.

### 1. Clone the Repository
```bash
git clone https://github.com/gagneet/awss3-sync.git
cd awss3-sync
```

### 2. Configure AWS Settings
Before running the application, create a configuration file named `appsettings.json` in the project's root directory (`AWSS3Sync/`).

**Important**: In Visual Studio, right-click the `appsettings.json` file in the Solution Explorer, select "Properties", and set **Copy to Output Directory** to **Copy if newer** or **Copy always**.

The file should contain your AWS credentials and S3 bucket details:
```json
{
  "AWS": {
    "AccessKey": "YOUR_AWS_ACCESS_KEY_ID",
    "SecretKey": "YOUR_AWS_SECRET_ACCESS_KEY",
    "Region": "your-s3-bucket-region",
    "BucketName": "your-s3-bucket-name"
  }
}
```

### 3. Build and Run in Visual Studio
1.  Open the `AWSS3Sync.sln` file in Visual Studio 2022.
2.  Visual Studio should automatically restore the required NuGet packages.
3.  Press `F5` or click the "Start" button to build and run the application.

> For a step-by-step walkthrough and troubleshooting, please refer to [USER_GUIDE.md](USER_GUIDE.md).

## 📦 Building a Standalone Application

You can publish the application as a standalone executable that can be run on any Windows machine, even without the .NET SDK installed.

### Using the Command Line (Recommended)
Open a terminal or PowerShell in the project's root directory (`AWSS3Sync/`) and use the `dotnet publish` command.

**To create a self-contained single-file executable for 64-bit Windows:**
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```
The output will be located in `bin/Release/net8.0-windows/win-x64/publish/`. The file `AWSS3Sync.exe` can be distributed and run.

**Command Explained:**
- `-c Release`: Builds the project in Release mode.
- `-r win-x64`: Specifies the target runtime is 64-bit Windows.
- `--self-contained true`: Includes the .NET runtime in the executable, so it doesn't need to be pre-installed on the target machine.
- `-p:PublishSingleFile=true`: Packages the application and its dependencies into a single `.exe` file.

### Using the Visual Studio Publish Wizard
1. **Right-click the `AWSS3Sync` project** in Solution Explorer → "Publish".
2. **Target**: Select "Folder" for local deployment.
3. **Location**: Choose a folder where the published files will be saved.
4. **Publish options**:
   - **Deployment mode**: `Self-contained`
   - **Target runtime**: `win-x64`
   - Check `Produce single file`.
5. **Publish**: Click "Publish" to generate the executable in the specified folder.

## 🔧 Troubleshooting

### "Configuration file not found"
- Ensure `appsettings.json` is in the same directory as the .exe after publishing.
- Check that "Copy to Output Directory" is set to "Copy if newer" or "Copy always" in Visual Studio.

### "AWS Access Denied"
- Verify your AWS credentials in `appsettings.json` are correct.
- Check the IAM permissions for the user associated with the credentials.
- Ensure the bucket name and region are correct.

### "Application won't start"
- If you published a framework-dependent version, ensure the correct .NET Desktop Runtime is installed on the target machine.
- Check the Windows Event Viewer for any application errors.

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
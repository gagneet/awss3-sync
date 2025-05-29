# AWS S3 Sync Utility

This .NET WinForms utility enables users to manage files in an AWS S3 bucket, including synchronization, uploads, downloads, and role-based access control for files intended for web display (e.g., on a WordPress site).

A comprehensive Windows desktop application for managing AWS S3 bucket files with role-based access control, hierarchical file navigation, and advanced permission management.

![Application Screenshot](https://img.shields.io/badge/Platform-Windows-blue) ![.NET](https://img.shields.io/badge/.NET-6.0+-purple) ![AWS](https://img.shields.io/badge/AWS-S3-orange)

## 🌟 Features

### 🔐 Role-Based Access Control
- **Administrator**: Full access to all files, upload/download/delete capabilities, permission management
- **Executive**: Can download files, upload to specific folders, access executive-only content
- **User**: View-only access to specifically assigned files and folders

### 🌲 Explorer-Like Interface
- **Hierarchical Tree View**: Navigate files and folders like Windows Explorer
- **Lazy Loading**: Efficient loading of large directory structures
- **Visual Icons**: Clear distinction between folders (📁) and files (📄)
- **Checkbox Selection**: Multi-select files and folders for batch operations

### 📁 File Operations
- **Upload**: Individual files or entire folder structures
- **Download**: Selective downloading with progress tracking
- **Sync**: Synchronize local folders with S3 bucket
- **Delete**: Enhanced confirmation system for permanent deletion

### 🛡️ Advanced Permission Management
- **Granular Control**: Set permissions per file or folder
- **Recursive Permissions**: Apply folder permissions to all contents
- **Dynamic Access**: Real-time permission changes
- **Executive Upload Zones**: Predefined folders for Executive uploads

## 🏗️ Architecture

### Project Structure
```
S3FileManager/
├── Models/
│   ├── UserRole.cs          # User roles and User class
│   ├── FileItem.cs          # Local and S3 file models
│   └── AppConfig.cs         # Configuration classes
├── Services/
│   ├── ConfigurationService.cs  # Config loading and validation
│   ├── S3Service.cs             # S3 operations with role filtering
│   ├── MetadataService.cs       # Permission management
│   └── FileService.cs           # Local file utilities
├── Forms/
│   ├── LoginForm.cs             # User authentication
│   ├── MainForm.cs              # Main application UI
│   ├── RoleSelectionForm.cs     # Permission assignment
│   ├── ProgressForm.cs          # Progress display
│   ├── PermissionManagementForm.cs  # Advanced permission control
│   ├── DeleteConfirmationForm.cs    # Enhanced delete confirmation
│   └── ExecutiveUploadFolderForm.cs # Executive upload destinations
└── Program.cs               # Application entry point
```

## 🚀 Quick Start

### Prerequisites
- Windows 10/11
- .NET 6.0 or later
- AWS Account with S3 access
- Visual Studio 2022 (for development)

### 1. Clone the Repository
```bash
git clone https://github.com/yourusername/aws-s3-file-manager.git
cd aws-s3-file-manager
```

### 2. Install Dependencies
Open the project in Visual Studio and install the required NuGet packages:
```xml
<PackageReference Include="AWSSDK.S3" Version="3.7.103.34" />
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
```

Or via Package Manager Console:
```powershell
Install-Package AWSSDK.S3
Install-Package Newtonsoft.Json
```

### 3. Configure AWS Settings
Create an `appsettings.json` file in the project root:
```json
{
  "AWS": {
    "AccessKey": "AKIA...YOUR_ACCESS_KEY",
    "SecretKey": "abcd...YOUR_SECRET_KEY", 
    "Region": "us-east-1",
    "BucketName": "your-bucket-name"
  }
}
```

**Important**: Set the file properties:
- Right-click `appsettings.json` in Solution Explorer
- Properties → Copy to Output Directory → "Copy always"

### 4. Build and Run
- Press `F5` or click "Start" in Visual Studio
- Login with your preferred role (Administrator recommended for initial setup)

## 📦 Publishing as Standalone Application

### Method 1: Visual Studio Publish Wizard

1. **Right-click the project** in Solution Explorer → "Publish"

2. **Choose Target**: Select "Folder" for local deployment

3. **Configure Publish Profile**:
   ```
   Target Framework: net6.0-windows
   Deployment Mode: Self-contained
   Target Runtime: win-x64 (or win-x86)
   File Publish Options:
   ✅ Produce single file
   ✅ Trim unused code
   ```

4. **Advanced Settings**:
   ```
   Configuration: Release
   Target Framework: net6.0-windows
   Deployment Mode: Self-contained
   Target Runtime: win-x64
   ✅ Ready to Run compilation
   ```

5. **Publish**: Click "Publish" to generate the executable

### Method 2: Command Line Publishing

Open Command Prompt/PowerShell in the project directory:

```bash
# Single file executable (recommended)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

# Framework-dependent (smaller size, requires .NET installed)
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true

# For x86 systems
dotnet publish -c Release -r win-x86 --self-contained true -p:PublishSingleFile=true
```

### Method 3: Create Installer (Advanced)

1. **Install Wix Toolset**: Download from https://wixtoolset.org/
2. **Add Wix Project**: Add a Setup Project to your solution
3. **Configure Installer**: Include all dependencies and the executable
4. **Build**: Generate MSI installer package

## 📋 Configuration Guide

### AWS Setup

1. **Create IAM User**:
   ```json
   {
     "Version": "2012-10-17",
     "Statement": [
       {
         "Effect": "Allow",
         "Action": [
           "s3:GetObject",
           "s3:PutObject",
           "s3:DeleteObject",
           "s3:ListBucket"
         ],
         "Resource": [
           "arn:aws:s3:::your-bucket-name",
           "arn:aws:s3:::your-bucket-name/*"
         ]
       }
     ]
   }
   ```

2. **S3 Bucket Configuration**:
   - Create bucket in your preferred region
   - Configure appropriate CORS if needed
   - Set up lifecycle policies for cost optimization

### Application Configuration

#### appsettings.json Location Priority:
1. Application directory (where .exe is located)
2. Current working directory
3. Base directory

#### File Permissions Storage:
- Permissions stored in `file_permissions.json`
- Automatically created in application directory
- JSON format for easy backup/restore

#### Executive Upload Folders:
Default allowed folders for Executive role:
- `executive-committee/`
- `reports/`
- `shared-documents/`

Modify in `ExecutiveUploadFolderForm.cs`:
```csharp
private readonly string[] _allowedFolders = { 
    "executive-committee", 
    "reports", 
    "shared-documents",
    "your-custom-folder" 
};
```

## 💡 Usage Guide

### User Roles

#### 👤 User Role
- **Access**: View files specifically assigned to User role
- **Capabilities**: Browse assigned files only
- **Restrictions**: Cannot upload, download, or delete

#### 👔 Executive Role  
- **Access**: View Executive and User-assigned files
- **Capabilities**: 
  - Download permitted files
  - Upload to specific folders (`executive-committee`, `reports`, `shared-documents`)
- **Restrictions**: Cannot delete files or manage permissions

#### 🔐 Administrator Role
- **Access**: Full access to all files and folders
- **Capabilities**:
  - Upload/download any files
  - Delete files with enhanced confirmation
  - Manage permissions for all files/folders
  - Create and modify role assignments
- **Special Features**: Permission management, recursive folder permissions

### Basic Workflow

1. **Login**: Select your role and enter username
2. **Browse Local Files**: Use "Browse Files/Folders" to select local content
3. **Navigate S3**: View your accessible S3 content in the tree view
4. **Select Items**: Use checkboxes to select files/folders
5. **Perform Operations**: Upload, download, or manage permissions based on your role

### Permission Management (Administrators)

1. **Select Files/Folders**: Check items in S3 tree view
2. **Click "Manage Permissions"**: Opens permission management dialog
3. **Set Access Roles**:
   - ✅ User Role: Basic users can view
   - ✅ Executive Role: Executives can download/upload
   - ✅ Administrator: Always enabled
4. **Apply**: Permissions applied recursively for folders

### Uploading Files

#### As Administrator:
1. Select local files → Upload → Choose access roles → Apply

#### As Executive:
1. Select local files → Upload → Choose destination folder → Apply
2. Files automatically get Executive + Administrator access

### Enhanced Deletion (Administrators Only)

1. **Select Items**: Check files/folders to delete
2. **Click "Delete Selected"**: Opens enhanced confirmation
3. **Review Items**: See exactly what will be deleted
4. **Type "DELETE"**: Required confirmation text
5. **Confirm**: Click "DELETE PERMANENTLY"

## 🔧 Troubleshooting

### Common Issues

#### "Configuration file not found"
- Ensure `appsettings.json` is in the same directory as the .exe
- Check that "Copy to Output Directory" is set to "Copy always"
- Verify JSON syntax is valid

#### "AWS Access Denied"
- Verify AWS credentials are correct
- Check IAM permissions for S3 bucket access
- Ensure bucket name is correct and accessible

#### "Application won't start"
- Install .NET 6.0 Runtime if using framework-dependent deployment
- Check Windows version compatibility
- Run as Administrator if needed

#### "Files not showing in tree"
- Check your user role permissions
- Verify files have appropriate role assignments
- Refresh the S3 file list

### Debug Mode

Enable debug logging by modifying the configuration:
```csharp
// In ConfigurationService.cs, add logging
Console.WriteLine($"Loading config from: {configPath}");
```

### Performance Optimization

For large buckets:
- Files load lazily in tree view
- Use S3 lifecycle policies for old files
- Consider pagination for very large folders

## 🔒 Security Considerations

### Best Practices

1. **AWS Credentials**:
   - Use IAM users with minimal required permissions
   - Rotate access keys regularly
   - Never commit credentials to source control

2. **Application Security**:
   - Run with least privilege user account
   - Keep .NET runtime updated
   - Regularly update AWS SDK

3. **Data Protection**:
   - Use S3 encryption at rest
   - Enable CloudTrail for audit logging
   - Implement bucket policies for additional security

### Permission Model

- **Metadata Storage**: Permissions stored locally in JSON format
- **Inheritance**: Folder permissions cascade to all contents
- **Default Access**: New files only accessible to Administrators initially
- **Role Validation**: All operations validate user role before execution

## 📝 Development

### Building from Source

1. **Clone Repository**
2. **Open in Visual Studio 2022**
3. **Restore NuGet Packages**
4. **Set Startup Project** to the main Windows Forms project
5. **Configure appsettings.json**
6. **Build and Run** (F5)

### Adding New Features

#### New User Role:
1. Add role to `UserRole` enum
2. Update permission checking logic in `S3Service`
3. Modify UI enable/disable logic in `MainForm`
4. Update role selection forms

#### New File Operations:
1. Add method to `S3Service`
2. Create UI controls in `MainForm`
3. Add progress tracking if needed
4. Update permission validation

### Testing

#### Manual Testing Checklist:
- [ ] Login with each role type
- [ ] Upload files with different permissions
- [ ] Download files based on role access
- [ ] Delete files (Administrator only)
- [ ] Manage permissions (Administrator only)
- [ ] Executive upload to allowed folders only

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Features

*   **File Operations:**
    *   Select a local folder and list its contents.
    *   Upload individual files or entire folders to an S3 bucket.
    *   Synchronize a local folder with an S3 bucket (uploads new/modified files).
    *   List files and folders from an S3 bucket.
    *   Download files from S3.
    *   Move S3 files to a backup location within the bucket.
*   **Role-Based Access Control (for WordPress Integration):**
    *   Allows an **Administrator** to manage file/folder visibility for different user roles on an external website (e.g., WordPress).
    *   Uses S3 Object Tagging with the tag `X-App-Role` to assign roles.
    *   Defined roles for tagging:
        *   `User`: Basic access. Can only see files/folders explicitly tagged for them.
        *   `Executive`: Can see all "User" tagged files and any files specifically tagged for "Executive". Can download. Upload/delete restricted to a specific `executive-committee/` folder (future enhancement for the app).
        *   `Admin`: Full access within the application. Can manage tags for "User" and "Executive" roles.
*   **Administrator Controls for Access Management:**
    *   **New Uploads:** When uploading files/folders, an "Grant User Role Access" checkbox allows the Administrator to tag items for the "User" role. If unchecked, items are tagged for "Admin" by default.
    *   **Existing S3 Objects:** A "Manage Access Roles" button allows the Administrator to select files or folders in the S3 listing and apply the "User" role tag. This is applied recursively for folders.

## File Structure

The project is organized into the following main directories:

*   `AWSS3Sync/` (Root project folder)
    *   `UI/`: Contains the Windows Forms UI code (e.g., `frmS3Sync.cs`).
    *   `Core/`: Contains the core logic.
        *   `S3/`: Houses `S3Service.cs`, which encapsulates all interactions with AWS S3.
        *   `Model/`: Contains data model classes, like `AppConstants.cs` for role tagging.
        *   `Utils/`: Contains utility classes like `Misc.cs`.
    *   `Properties/`: Standard .NET project properties.
    *   `bin/`: Output directory for compiled application.

## Setup and Build Instructions

### Prerequisites

*   **.NET Framework 4.7.2 Developer Pack:** This application targets .NET Framework 4.7.2. You'll need the developer pack installed.
*   **MSBuild:** Required for building the project. It usually comes with Visual Studio or the .NET Framework SDK.
*   **AWS Account and S3 Bucket:** You need an active AWS account and an S3 bucket.
*   **AWS Credentials:** Valid AWS Access Key ID and Secret Access Key with permissions to access your S3 bucket.

### Configuration (`appsettings.json`)

Before building and running the application, you must create a configuration file named `appsettings.json` in the root directory of the project (e.g., `AWSS3Sync/AWSS3Sync/appsettings.json` if your solution structure has an extra nested project folder, or alongside `AWSS3Sync.csproj` if it's flatter).

**Important:** Ensure this file's "Copy to Output Directory" property in Visual Studio is set to "Copy if newer" or "Copy always".

Provide your AWS details in this file:

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

Replace the placeholder values with your actual credentials and S3 details. For example:
*   `Region`: `us-east-1`, `ap-southeast-2`, etc.
*   `BucketName`: The name of your S3 bucket.

### Building and Running

#### Visual Studio 2022 (Recommended)

1.  Clone the repository.
2.  Open the `awss3-sync.sln` file in Visual Studio 2022.
3.  Ensure `appsettings.json` is created and configured as described above.
4.  Build the solution (Build > Build Solution or Ctrl+Shift+B).
5.  Run the application (Debug > Start Debugging or F5).

#### Visual Studio Code (with .NET CLI or MSBuild)

1.  Clone the repository.
2.  Ensure `appsettings.json` is created and configured.
3.  **Editing:** VS Code provides excellent C# editing support with the C# extension.
4.  **Building:**
    *   .NET Framework WinForms projects are not natively built using `dotnet build` in the same way as .NET Core/.NET 5+ projects.
    *   You'll typically need to use MSBuild directly. Open a Developer Command Prompt for Visual Studio (or ensure MSBuild is in your PATH).
    *   Navigate to the project directory (containing `AWSS3Sync.csproj`) and run:
        ```bash
        msbuild AWSS3Sync.csproj /p:Configuration=Debug /p:Platform=AnyCPU
        ```
        (Or `/p:Configuration=Release`)
    *   The compiled application will be in the `bin/Debug/` or `bin/Release/` folder.
5.  **Running:** Execute the `.exe` file from the output folder.

## Using the Application

*   **Browse Local Folder:** Use "Browse Folder" to select a local directory. Its files will be listed.
*   **Upload File:** Use "Browse & Upload" to select a single file and upload it.
    *   *(Admin)* Check/uncheck "Grant User Role Access" before upload to set appropriate S3 tags.
*   **Upload Folder:** Use "Folder Upload" to upload all contents of the selected local folder.
    *   *(Admin)* The "Grant User Role Access" checkbox applies to all files in the folder.
*   **Sync Folder:** Use "Sync & Upload" to synchronize the selected local folder with S3 (based on file timestamps and existence).
    *   *(Admin)* The "Grant User Role Access" checkbox applies to newly uploaded/updated files.
*   **List S3 Files:** Use "List Files" to view objects in your S3 bucket.
*   **Download S3 File:** Select a file in the S3 list and click "Download".
*   **Manage Access Roles (Admin):**
    *   Select one or more files/folders in the S3 list.
    *   Click "Manage Access Roles".
    *   Confirm to grant "User" role access. Tags will be applied (recursively for folders).
*   **Move to Backup (Delete):** Select files in the S3 list and click "Delete Files" (this moves them to a backup subfolder in S3, not a permanent delete).

## 🤝 Contributing

Feel free to fork this repository, make enhancements, and submit pull requests.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 📞 Support

For support and questions:
- Create an issue on GitHub
- Check the troubleshooting section above
- Review AWS S3 documentation for bucket configuration

## 🗺️ Roadmap

### Planned Features:
- [ ] User management with database backend
- [ ] Audit logging and activity tracking
- [ ] File versioning support
- [ ] Bulk operations with better progress tracking
- [ ] Search and filtering capabilities
- [ ] Integration with Active Directory
- [ ] Mobile companion app
- [ ] Advanced encryption options

### Version History:
- **v1.0.0**: Initial release with basic file operations
- **v1.1.0**: Added role-based access control
- **v1.2.0**: Enhanced TreeView interface
- **v1.3.0**: Advanced permission management and Executive role features

---

**Made with ❤️ for efficient AWS S3 file management**

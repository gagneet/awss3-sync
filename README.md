# AWS S3 Sync Utility

A comprehensive Windows desktop application for managing AWS S3 bucket files with role-based access control, hierarchical file navigation, and advanced permission management.

![Application Screenshot](https://img.shields.io/badge/Platform-Windows-blue) ![.NET](https://img.shields.io/badge/.NET-6.0+-purple) ![AWS](https://img.shields.io/badge/AWS-S3-orange)

## 🌟 Features

### 🔐 Role-Based Access Control
- **Administrator**: Full access to all files, upload/download/delete capabilities, permission management
- **Executive**: Can download files, upload to specific folders, access executive-only content
- **User**: View-only access to specifically assigned files and folders

### 🌲 Explorer-Like Interface
- **Hierarchical Tree View**: Navigate local and S3 files and folders in a familiar parent-child structure.
- **Lazy Loading for Local Files**: Efficiently browse large local directories without long initial load times.
- **File Previewer**: Click on a file to see a preview of its content directly in the application. Supports common text and image formats.
- **Visual Icons**: Clear distinction between folders (📁) and files (📄).
- **Checkbox Selection**: Multi-select files and folders for batch operations like uploading, downloading, and deleting.

### 📁 File Operations
- **Upload**: Individual files or entire folder structures
- **Download**: Selective downloading of files and folders.
- **Sync**: Synchronize local folders with an S3 bucket (Note: This feature is currently not fully implemented).
- **Delete**: Enhanced confirmation system for permanent deletion.

### 🛡️ Advanced Permission Management
- **Granular Control**: Set permissions per file or folder.
- **Recursive Permissions**: Apply folder permissions to all contents.
- **Dynamic Access**: Real-time permission changes.
- **Executive Upload Zones**: Predefined folders for Executive uploads.

## 🚀 Getting Started

### Prerequisites
- Windows 10/11
- .NET 8.0 SDK or later (as specified in the build workflow)
- Visual Studio 2022 (for development)
- An active AWS Account with an S3 bucket and IAM user credentials.

### 1. Clone the Repository
```bash
git clone https://github.com/your-repo/AWSS3Sync.git
cd AWSS3Sync
```

### 2. Configure AWS Settings
Before running the application, you must create a configuration file named `appsettings.json` in the project's root directory (`AWSS3Sync/`).

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

## 🤝 Contributing

Feel free to fork this repository, make enhancements, and submit pull requests.
1. Fork the repository.
2. Create your feature branch (`git checkout -b feature/YourAmazingFeature`).
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`).
4. Push to the branch (`git push origin feature/YourAmazingFeature`).
5. Open a Pull Request.

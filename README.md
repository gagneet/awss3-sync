# Modernized AWS S3 Sync Utility

A robust, performant, and secure Windows desktop application for bidirectional synchronization between local directories and AWS S3 buckets.

## 🚀 Key Features

- **Bidirectional Sync**: Advanced 3-state sync engine (Local, Remote, Snapshot) using SQLite for state tracking.
- **Modern UI**: Professional dual-pane interface built with the **Krypton Toolkit**.
- **High Performance**: Parallel transfers, multipart uploads, and metadata caching for maximum efficiency.
- **Responsive Interface**: Async lazy-loading TreeViews ensure the UI never hangs during directory browsing.
- **Enhanced Security**: Secure credential storage using **Windows Credential Manager** and **DPAPI**.
- **Unified Authentication**: Support for both **AWS Cognito** and **Local Login** modes.
- **Bandwidth Management**: Integrated throttling support.
- **Scheduled Operations**: Background sync support via **Quartz.NET**.

## 🔐 Authentication & Credentials

The application supports two login modes:

### 1. AWS Cognito (Recommended)
Uses AWS Cognito User Pools for authentication. This mode provides full S3 access based on IAM roles.
- Requires Cognito configuration in `appsettings.json`.

### 2. Local Login (Fallback)
Allows access without AWS Cognito. Useful for testing or when Cognito is unavailable.
- **Default Credentials**:
    - **Administrator**: `admin` / `admin`
    - **Executive**: `exec` / `exec`
    - **User**: `user` / `user`
- Note: Local login provides limited functionality unless manual AWS credentials are also provided in `appsettings.json`.

## 🛠 Project Structure

- **FileSyncApp.Core**: Business logic, sync engine, interfaces, and shared models.
- **FileSyncApp.S3**: AWS S3 and Cognito service implementations.
- **FileSyncApp.WinForms**: Modern MVP-based UI implementation and DI container setup.
- **FileSyncApp.Tests**: Comprehensive unit tests for the sync engine and core logic.

## 📋 Pre-requisites

- **.NET 8.0 SDK** or later.
- **Visual Studio 2022** (recommended) with "Desktop development with .NET" workload.
- **AWS Account** with S3 bucket and Cognito User/Identity Pools configured.
- **Windows 10/11** (required for DPAPI and Windows Credential Manager).

## 🔨 Build Instructions

### Pre-requisites
- **.NET 8.0 SDK** or later.
- **Visual Studio 2022** with the **.NET Desktop Development** workload installed.
- An **AWS Account** with an S3 bucket and Cognito User Pool/Identity Pool if using AWS authentication.

### Using Visual Studio
1. Open `FileSyncApp.sln`.
2. Wait for NuGet packages to restore automatically (or right-click solution -> Restore NuGet Packages).
3. Ensure all projects are targeted to **.NET 8.0**.
4. Set `FileSyncApp.WinForms` as the Startup Project.
5. Press **F5** to build and run.

### Troubleshooting Build Issues
- If you get "Metadata file could not be found" errors, try **Clean Solution** and then **Rebuild Solution**.
- Ensure `System.IO` and `System.Linq` namespaces are available (the project uses ImplicitUsings, but explicit usings have been added to key files for compatibility).

### Using .NET CLI
```bash
dotnet restore
dotnet build FileSyncApp.sln
dotnet run --project FileSyncApp.WinForms/FileSyncApp.WinForms.csproj
```

## ⚙️ Configuration

Before running, update `FileSyncApp.WinForms/appsettings.json` with your AWS and Cognito details:

```json
{
  "AWS": {
    "AccessKey": "YOUR_ACCESS_KEY",
    "SecretKey": "YOUR_SECRET_KEY",
    "Region": "us-east-1",
    "BucketName": "your-s3-bucket"
  },
  "Cognito": {
    "Region": "us-east-1",
    "UserPoolId": "us-east-1_xxxxxx",
    "ClientId": "xxxxxxxxxxxxxxxx",
    "IdentityPoolId": "us-east-1:xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
  },
  "Performance": {
    "MaxConcurrentUploads": 5,
    "MaxBytesPerSecond": 0
  }
}
```

## 🧪 Running Tests
```bash
dotnet test FileSyncApp.sln
```

## 📜 License
This project is licensed under the MIT License.

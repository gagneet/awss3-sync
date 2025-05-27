# AWS S3 Sync Utility

This .NET WinForms utility enables users to manage files in an AWS S3 bucket, including synchronization, uploads, downloads, and role-based access control for files intended for web display (e.g., on a WordPress site).

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

## Contributing

Feel free to fork this repository, make enhancements, and submit pull requests.

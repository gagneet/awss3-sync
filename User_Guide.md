\# AWS S3 Sync Utility - User Guide



Welcome to the AWS S3 Sync Utility! This guide will walk you through installation, configuration, and using all major features of the application.



---



\## Table of Contents



1\. Introduction

2\. Installation

3\. Configuration

4\. Launching the App

5\. Logging In

6\. Navigating the Interface

7\. Uploading, Downloading \& Deleting Files

8\. Syncing Files

9\. Managing Permissions \& Tagging

10\. Troubleshooting



---



\## 1. Introduction



This Windows desktop app helps you manage files between your computer and AWS S3 buckets, with secure, role-based access.



---



\## 2. Installation



\*\*Prerequisites:\*\*

\- Windows 10/11

\- .NET 8.0 Runtime (or self-contained EXE)

\- AWS account with S3 bucket and IAM credentials



\*\*How to Install:\*\*

1\. Download the published EXE or build from source (see README).

2\. Place the application in your preferred folder.



---



\## 3. Configuration



1\. Create `appsettings.json` in the same directory as `AWSS3Sync.exe`.

2\. Add AWS credentials and S3 details (see README for format).

3\. Ensure `appsettings.json` is set to "Copy if newer" in Visual Studio if building from source.



---



\## 4. Launching the App



Double-click `AWSS3Sync.exe`. The login window appears.



---



\## 5. Logging In



Select your user role and enter credentials.



\*\*Roles:\*\*

\- \*\*Administrator:\*\* Full access

\- \*\*Executive:\*\* Limited upload/download

\- \*\*User:\*\* View-only



---



\## 6. Navigating the Interface



\- \*\*Tree View:\*\* Browse local and S3 files/folders.

\- \*\*File Preview:\*\* Select a file to view its content.

\- \*\*Batch Operations:\*\* Use checkboxes to select multiple files.



> !\[Sample Screenshot: Main Window Tree View](images/main-window-treeview.png)

> \*Annotated: Tree Panel, Preview Panel, Action Buttons\*



---



\## 7. Uploading, Downloading \& Deleting Files



\- \*\*Upload:\*\* Select files/folders, click "Upload".

\- \*\*Download:\*\* Select files/folders, click "Download".

\- \*\*Delete:\*\* Select files, click "Delete" (confirm action).



---



\## 8. Syncing Files



\- Click "Sync" to synchronize local and S3 folders.

\- \*Note: The sync feature is in development—see README for limitations.\*



---



\## 9. Managing Permissions \& Tagging



\- Right-click files/folders to set permissions and tags.

\- Permissions may be set recursively for folders.

\- Tags help identify content for users/roles.



> !\[Sample Screenshot: Permission Dialog](images/permission-dialog.png)

> \*Annotated: User Selection, Role Assignment, Recursive Checkbox\*



---



\## 10. Troubleshooting



\- \*\*Configuration file not found:\*\* Ensure `appsettings.json` is present.

\- \*\*AWS Access Denied:\*\* Check credentials and bucket permissions.

\- \*\*App won’t launch:\*\* Verify .NET runtime or use self-contained EXE.



---



\## Need Help?



Contact your administrator or refer to the README for common solutions.


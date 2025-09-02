# AWS S3 Sync Utility - User Guide

Welcome to the AWS S3 Sync Utility! This guide will walk you through installation, configuration, and using all major features of the application.

---

## Table of Contents

1. [Introduction](#1-introduction)  
2. [Installation](#2-installation)  
3. [Configuration](#3-configuration)  
4. [Launching the App](#4-launching-the-app)  
5. [Logging In](#5-logging-in)  
6. [Navigating the Interface](#6-navigating-the-interface)  
7. [Uploading, Downloading & Deleting Files](#7-uploading-downloading--deleting-files)  
8. [Syncing Files](#8-syncing-files)  
9. [Managing Permissions & Tagging](#9-managing-permissions--tagging)  
10. [First-Run Setup for Administrators](#10-first-run-setup-for-administrators)  
11. [Troubleshooting](#11-troubleshooting)  
12. [Need Help?](#need-help)

---

## 1. Introduction

This Windows desktop app helps you manage files between your computer and AWS S3 buckets, with secure, role-based access.

---

## 2. Installation

**Prerequisites:**

- Windows 10/11  
- .NET 8.0 Runtime (or self-contained EXE)  
- AWS account with S3 bucket and IAM credentials

**How to Install:**

1. Download the published EXE or build from source (see README)  
2. Place the application in your preferred folder

---

## 3. Configuration

1. Create `appsettings.json` in the same directory as `AWSS3Sync.exe`  
2. Add AWS credentials and S3 details (see README for format)  
3. Ensure `appsettings.json` is set to "Copy if newer" in Visual Studio if building from source

---

## 4. Launching the App

Double-click `AWSS3Sync.exe`. The login window appears.

---

## 5. Logging In

The application uses a **Unified Authentication System** that provides secure, intelligent login with automatic method detection.

### Authentication Methods

**Automatic (Recommended):** The system automatically tries AWS Cognito first, then falls back to local authentication if needed.

**AWS Cognito Authentication:**
- Full AWS integration with proper S3 permissions
- Role-based access via AWS IAM groups
- Offline mode support with cached credentials
- Secure temporary credentials for S3 operations

**Local Authentication:**
- Simple username-based authentication for backward compatibility
- Limited S3 access (operations may fail without AWS credentials)
- Manual role selection
- Should be used only when Cognito is unavailable

### Login Process

1. **Launch the application** - The Unified Login Form appears
2. **Select authentication method** (Automatic recommended)
3. **Enter your credentials:**
   - Username or email address
   - Password
4. **Optional settings:**
   - Check "Try offline mode first" if you have cached Cognito credentials
5. **Click Login**

### Security Warnings

- **Limited Access Warning:** If you authenticate locally without AWS credentials, you'll see a warning about limited S3 access
- **Offline Mode Notice:** When using cached credentials, some features may be limited
- **Credential Validation:** The system validates AWS credentials before allowing S3 operations

### User Roles

**Administrator:** 
- Full S3 bucket access (read/write/delete)
- All administrative functions
- AWS Cognito group: `strata-admin` or `Administrator`

**Executive:** 
- Upload and download access
- Limited administrative functions  
- AWS Cognito group: `strata-ec`, `Executive`, or `ExecutiveCommittee`

**User (Resident):** 
- View and download access for public folders
- AWS Cognito group: Default for all users

### Troubleshooting Login Issues

**"No AWS credentials available" error:**
- Contact your administrator to set up AWS Cognito authentication
- Verify your internet connection for Cognito access

**"Authentication failed" error:**
- Check your username and password
- Try different authentication methods
- Ensure AWS Cognito service is available

**"Limited access mode" warning:**
- You're authenticated locally but don't have AWS credentials
- Contact your administrator for full AWS Cognito access
- Some S3 operations may fail

### Getting Help

Click **"Need help?"** on the login form for detailed troubleshooting guidance and authentication method explanations.

---

## 6. Navigating the Interface

- **Tree View:** Browse local and S3 files/folders  
- **File Preview:** Select a file to view its content  
- **Batch Operations:** Use checkboxes to select multiple files

> _Sample Screenshot: Main Window Tree View_  
> _Annotated: Tree Panel, Preview Panel, Action Buttons_

---

## 7. Uploading, Downloading & Deleting Files

- **Upload:** Select files/folders, click "Upload"  
- **Download:** Select files/folders, click "Download"  
- **Delete:** Select files, click "Delete" (confirm action)

---

## 8. Syncing Files

- Click "Sync" to synchronize local and S3 folders  
- _Note: The sync feature is in development—see README for limitations._

---

## 9. Managing Permissions & Tagging

### For All Users
- Select files/folders from the S3 panel and click "Manage Permissions" to view current access settings
- Permissions control who can view, download, and upload files
- Files are automatically tagged with permission metadata for security

### For Administrators
- **Review Pending Permissions:** Click the "Review Permissions" button (yellow button in S3 panel) to see files requiring attention
- **Auto-Tagging System:** Files without proper permission tags are automatically assigned "Permission: pending" status
- **Bulk Permission Management:** Select multiple files from the pending list to set permissions efficiently
- **Permission Workflow:**
  1. Files uploaded or synced without permission tags receive "pending" status
  2. Use "Review Permissions" to see all pending files
  3. Select files and click "Set Permissions" to assign proper access roles
  4. Use "Clear All" to acknowledge review completion

### Permission Types
- **User Role:** Basic read-only access to assigned files
- **Executive Role:** Download access plus upload to specific folders
- **Administrator Role:** Full access to all files and permission management

> **Note:** Permissions are applied recursively to folder contents. The system automatically ensures all files have proper permission metadata for security compliance.

---

## 10. First-Run Setup for Administrators

When using the application for the first time or after adding new files to S3:

1. **Initial File Review:** The application will automatically detect files missing permission tags
2. **Review Notification:** A yellow "Review Permissions" button will appear for administrators
3. **Permission Assignment:** Click the button to see all files requiring permission review
4. **Set Appropriate Access:** Select files and assign User, Executive, or Administrator access as needed
5. **Complete Setup:** Clear the pending list after assigning permissions

This ensures all files have proper security metadata and users can access only authorized content.

---

## 11. Troubleshooting

- **Configuration file not found:** Ensure `appsettings.json` is present  
- **AWS Access Denied:** Check credentials and bucket permissions  
- **App won’t launch:** Verify .NET runtime or use self-contained EXE

---

## Need Help?

Contact your administrator or refer to the README for common solutions.
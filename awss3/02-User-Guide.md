# FileSyncApp User Guide

## Getting Started

### System Requirements

- Windows 10/11 (64-bit)
- .NET 8.0 Runtime (bundled with installer)
- Internet connection for AWS S3 access
- Minimum 4GB RAM, 100MB disk space

### Installation

1. Download `FileSyncApp-Setup.exe` from the provided link
2. Run the installer and follow the prompts
3. Launch FileSyncApp from the Start menu or desktop shortcut

### First-Time Setup

1. **Login**: Enter your credentials provided by your administrator
2. **Configure Local Folder**: Click "Browse" to select your local sync folder
3. **Verify Connection**: The S3 bucket contents will appear in the right panel

---

## Main Interface

```
┌─────────────────────────────────────────────────────────────────────────┐
│  FileSyncApp - Strata Document Manager                            _ □ X │
├─────────────────────────────────────────────────────────────────────────┤
│  [Sync Now] [Settings] [Refresh]                          🔄 Connected  │
├────────────────────────────┬────────────────────────────────────────────┤
│                            │                                            │
│   📁 Local Files           │   ☁️ AWS S3 Bucket                         │
│   ─────────────────────    │   ─────────────────────                    │
│   ▼ 📁 Documents           │   ▼ 📁 Financial                           │
│     ▼ 📁 Financial         │     📄 Budget-2024.xlsx                    │
│       📄 Budget-2024.xlsx  │     📄 Levy-Schedule.pdf                   │
│       📄 Invoice-001.pdf   │   ▼ 📁 Minutes                             │
│     ▼ 📁 Minutes           │     📄 AGM-2024.docx                       │
│       📄 AGM-2024.docx     │   ▼ 📁 Maintenance                         │
│                            │     📄 Work-Order-123.pdf                  │
│                            │                                            │
├────────────────────────────┴────────────────────────────────────────────┤
│  Status: Ready | Last sync: 2 hours ago | 3,342 files | 2.4 GB         │
└─────────────────────────────────────────────────────────────────────────┘
```

### Panel Overview

| Panel | Description |
|-------|-------------|
| **Left Panel (Local)** | Shows files and folders on your computer |
| **Right Panel (S3)** | Shows files and folders in the cloud storage |
| **Status Bar** | Displays sync status, file counts, and storage used |

---

## Core Features

### 1. Syncing Files

#### Full Bidirectional Sync

Click **[Sync Now]** to synchronize all files between local and S3:

- **Local → S3**: New or modified local files are uploaded
- **S3 → Local**: New or modified cloud files are downloaded
- **Deletions**: Files deleted on either side are removed from the other

#### Sync Preview

Before syncing, a preview dialog shows planned actions:

```
┌─────────────────────────────────────────────────┐
│           Sync Preview                          │
├─────────────────────────────────────────────────┤
│  ⬆️ Upload:    12 files (45 MB)                 │
│  ⬇️ Download:   3 files (8 MB)                  │
│  🗑️ Delete:     1 file                          │
│  ⚠️ Conflicts:  2 files                         │
├─────────────────────────────────────────────────┤
│  [View Details]  [Resolve Conflicts]  [Sync]    │
└─────────────────────────────────────────────────┘
```

### 2. Downloading Files

#### Single File Download

1. Right-click a file in the S3 panel
2. Select **"Download"**
3. Choose save location

#### Folder Download (as ZIP)

1. Right-click a folder in the S3 panel
2. Select **"Download as ZIP"**
3. Choose save location for the ZIP file

#### Multiple File Download

1. Hold **Ctrl** and click to select multiple files
2. Right-click and select **"Download Selected"**
3. Files are downloaded preserving folder structure

### 3. Uploading Files

#### Drag and Drop

Simply drag files from Windows Explorer to the S3 panel to upload them.

#### Upload Dialog

1. Click **"Upload"** in the toolbar
2. Select files or folders
3. Choose destination folder in S3
4. Click **"Start Upload"**

### 4. Conflict Resolution

When a file is modified both locally and in S3, a conflict occurs:

```
┌─────────────────────────────────────────────────────────────────┐
│                    Resolve Conflict                              │
├─────────────────────────────────────────────────────────────────┤
│  File: Financial/Budget-2024.xlsx                               │
│                                                                  │
│  ┌─────────────────────┐    ┌─────────────────────┐             │
│  │   Local Version     │    │   Cloud Version     │             │
│  │   Modified: Today   │    │   Modified: Yesterday│            │
│  │   Size: 245 KB      │    │   Size: 238 KB      │             │
│  │   By: You           │    │   By: John Smith    │             │
│  └─────────────────────┘    └─────────────────────┘             │
│                                                                  │
│  ○ Keep Local (upload your version)                             │
│  ○ Keep Cloud (download their version)                          │
│  ○ Keep Both (rename local with timestamp)                      │
│  ○ Compare (open both versions)                                 │
│                                                                  │
│  ☑ Apply to all similar conflicts                               │
│                                                                  │
│  [Cancel]                                          [Apply]       │
└─────────────────────────────────────────────────────────────────┘
```

#### Conflict Policies

| Policy | Description |
|--------|-------------|
| **Newer Wins** | Automatically keep whichever version was modified last |
| **Local Wins** | Always prefer your local version |
| **Remote Wins** | Always prefer the cloud version |
| **Keep Both** | Rename conflicting file with timestamp suffix |
| **Prompt** | Ask for each conflict (default) |

---

## Settings

Access via **[Settings]** button or **File → Settings**.

### General Settings

| Setting | Description |
|---------|-------------|
| **Local Sync Folder** | Root folder on your computer to sync |
| **Auto-Sync Interval** | How often to automatically sync (Off, 15min, 30min, 1hr) |
| **Start with Windows** | Launch FileSyncApp when you log in |
| **Minimize to Tray** | Keep running in system tray when closed |

### Sync Settings

| Setting | Description |
|---------|-------------|
| **Conflict Policy** | How to handle file conflicts (see above) |
| **Exclude Patterns** | File patterns to skip (e.g., `*.tmp`, `Thumbs.db`) |
| **Delete Sync** | Whether to sync deletions (enabled by default) |

### Performance Settings

| Setting | Description |
|---------|-------------|
| **Concurrent Transfers** | Number of simultaneous uploads/downloads (1-10) |
| **Bandwidth Limit** | Maximum upload/download speed (KB/s, 0 = unlimited) |
| **Schedule Throttling** | Different limits for business hours vs. off-hours |

### Security Settings

| Setting | Description |
|---------|-------------|
| **Remember Login** | Stay logged in between sessions |
| **Lock Timeout** | Auto-lock after inactivity (Off, 5min, 15min, 30min) |

---

## File Operations

### Context Menu (Right-Click)

#### Local Panel Context Menu
- **Upload to S3** - Upload selected files/folders
- **Open** - Open file with default application
- **Open Folder** - Open containing folder in Explorer
- **Exclude from Sync** - Add to exclusion list
- **Properties** - View file details

#### S3 Panel Context Menu
- **Download** - Download to local folder
- **Download as ZIP** - Download folder as archive
- **Open** - Stream and open file
- **Copy Link** - Copy shareable S3 URL (if permitted)
- **Delete** - Remove from S3 (requires confirmation)
- **Properties** - View S3 object details

### Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| **F5** | Refresh both panels |
| **Ctrl+S** | Start sync |
| **Ctrl+D** | Download selected |
| **Ctrl+U** | Upload selected |
| **Delete** | Delete selected (with confirmation) |
| **Ctrl+F** | Search files |
| **Escape** | Cancel current operation |

---

## Transfer Queue

View ongoing and queued transfers via **View → Transfer Queue**.

```
┌─────────────────────────────────────────────────────────────────┐
│  Transfer Queue                                            _ □ X │
├─────────────────────────────────────────────────────────────────┤
│  ⬆️ Uploading: Budget-2024.xlsx                                 │
│     [████████████░░░░░░░░] 65% - 2.1 MB/s - ETA: 12s            │
│                                                                  │
│  Queued (3):                                                     │
│    ⬆️ Invoice-March.pdf          1.2 MB     Pending              │
│    ⬆️ Minutes-Feb.docx           450 KB     Pending              │
│    ⬇️ Work-Order-456.pdf         890 KB     Pending              │
│                                                                  │
│  Completed (12):                                                 │
│    ✅ Report-Q1.xlsx             3.4 MB     00:04                │
│    ✅ Photo-001.jpg              2.1 MB     00:02                │
│    ...                                                           │
├─────────────────────────────────────────────────────────────────┤
│  [Pause All]  [Resume All]  [Clear Completed]                    │
└─────────────────────────────────────────────────────────────────┘
```

### Transfer Controls

- **Pause/Resume**: Pause individual or all transfers
- **Cancel**: Stop a transfer (partial uploads are discarded)
- **Retry**: Retry failed transfers
- **Priority**: Move items up/down in queue

---

## Troubleshooting

### Connection Issues

**"Unable to connect to AWS S3"**
1. Check your internet connection
2. Verify VPN is connected (if required)
3. Try **[Refresh]** to reconnect
4. Contact your administrator if issues persist

**"Session expired"**
1. Click **"Re-authenticate"** in the dialog
2. Enter your credentials again

### Sync Issues

**"File locked by another application"**
- Close the application using the file
- Try sync again

**"Access denied"**
- You may not have permission for this file/folder
- Contact your administrator

**"File too large"**
- Files over 5GB require multipart upload
- This happens automatically but may take longer

### Performance Issues

**"Sync is slow"**
1. Check bandwidth limit in Settings
2. Reduce concurrent transfers if network is congested
3. Consider syncing during off-peak hours

**"Application not responding"**
- Large folder operations may take time
- Wait for the status bar to show "Ready"
- If frozen for more than 2 minutes, use Task Manager to close

---

## Security Best Practices

1. **Never share your login credentials**
2. **Lock your computer** when away from desk
3. **Report suspicious activity** to your administrator
4. **Don't sync sensitive files** to personal devices
5. **Use strong passwords** (12+ characters, mixed case, numbers)

---

## Getting Help

### In-App Help
- Press **F1** for context-sensitive help
- Click **Help → User Guide** for this document

### Support
- Email: support@yourstrata.com.au
- Phone: 1800-XXX-XXX (business hours)

### Reporting Issues
Include the following when reporting problems:
1. What you were trying to do
2. The exact error message
3. Log file from `%APPDATA%\FileSyncApp\logs\`

---

*Version 1.0 | February 2026*

# FileSyncApp Architecture Document

## Executive Summary

FileSyncApp is a Windows desktop application that provides bidirectional file synchronization between local filesystem and AWS S3, with role-based access control designed for strata building document management.

---

## Solution Architecture

### High-Level System Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              FileSyncApp                                     │
├─────────────────────────────────────────────────────────────────────────────┤
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                    Presentation Layer (WinForms)                     │    │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌────────────┐  │    │
│  │  │  MainForm   │  │SettingsForm│  │ ConflictDlg │  │ProgressDlg│  │    │
│  │  │  (IView)    │  │             │  │             │  │            │  │    │
│  │  └──────┬──────┘  └─────────────┘  └─────────────┘  └────────────┘  │    │
│  └─────────┼───────────────────────────────────────────────────────────┘    │
│            │                                                                 │
│  ┌─────────▼───────────────────────────────────────────────────────────┐    │
│  │                    Business Logic Layer (Core)                       │    │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌────────────┐  │    │
│  │  │ SyncEngine  │  │ FileSyncPre-│  │ Conflict-   │  │ Scheduler  │  │    │
│  │  │             │  │ senter      │  │ Resolver    │  │ Service    │  │    │
│  │  └──────┬──────┘  └─────────────┘  └─────────────┘  └────────────┘  │    │
│  │         │                                                            │    │
│  │  ┌──────▼──────────────────────────────────────────────────────┐    │    │
│  │  │                    Service Interfaces                        │    │    │
│  │  │  IFileStorageService │ IAuthService │ IConfigurationService  │    │    │
│  │  └──────────────────────────────────────────────────────────────┘    │    │
│  └──────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │                    Infrastructure Layer                               │   │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌────────────┐   │   │
│  │  │S3FileStorage│  │ LocalFile-  │  │ Cognito-    │  │ SQLite     │   │   │
│  │  │Service      │  │ Service     │  │ AuthService │  │ Metadata   │   │   │
│  │  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘  └─────┬──────┘   │   │
│  └─────────┼────────────────┼────────────────┼───────────────┼──────────┘   │
└────────────┼────────────────┼────────────────┼───────────────┼──────────────┘
             │                │                │               │
     ┌───────▼────────┐ ┌─────▼─────┐  ┌──────▼──────┐ ┌──────▼──────┐
     │    AWS S3      │ │  Local    │  │   AWS       │ │  SQLite     │
     │    Bucket      │ │Filesystem │  │  Cognito    │ │  Database   │
     └────────────────┘ └───────────┘  └─────────────┘ └─────────────┘
```

---

## Project Structure

```
FileSyncApp.sln
├── FileSyncApp.Core/                    # Business logic - NO UI dependencies
│   ├── Interfaces/
│   │   ├── IFileStorageService.cs       # Abstraction for S3/Local storage
│   │   ├── IAuthService.cs              # Authentication contract
│   │   ├── IConfigurationService.cs     # Configuration management
│   │   ├── ISyncEngine.cs               # Sync orchestration
│   │   └── IFileSyncView.cs             # MVP view interface
│   ├── Models/
│   │   ├── FileNode.cs                  # File/folder representation
│   │   ├── SyncOperation.cs             # Upload/download/delete action
│   │   ├── SyncResult.cs                # Sync outcome
│   │   ├── ConflictInfo.cs              # Conflict details
│   │   ├── UserSession.cs               # Authenticated user
│   │   └── AppConfiguration.cs          # Settings model
│   ├── Services/
│   │   ├── SyncEngine.cs                # Core sync algorithm
│   │   ├── FileComparer.cs              # Hash/date comparison
│   │   ├── ConflictResolver.cs          # Conflict strategies
│   │   ├── ThrottledStream.cs           # Bandwidth limiting
│   │   └── MetadataCache.cs             # SQLite-backed file index
│   └── Presenters/
│       └── FileSyncPresenter.cs         # MVP presenter
│
├── FileSyncApp.S3/                      # AWS S3 implementation
│   ├── Services/
│   │   ├── S3FileStorageService.cs      # IFileStorageService for S3
│   │   ├── S3MetadataService.cs         # S3 object tagging
│   │   └── CognitoAuthService.cs        # Cognito authentication
│   └── FileSyncApp.S3.csproj
│
├── FileSyncApp.WinForms/                # UI layer
│   ├── Forms/
│   │   ├── MainForm.cs                  # Primary dual-pane interface
│   │   ├── SettingsForm.cs              # Configuration UI
│   │   ├── ConflictResolutionDialog.cs  # Conflict handling UI
│   │   └── ProgressDialog.cs            # Transfer progress
│   ├── Controls/
│   │   ├── AsyncTreeView.cs             # Lazy-loading tree
│   │   └── TransferQueueControl.cs      # Download/upload queue
│   ├── Program.cs                       # DI setup, entry point
│   └── FileSyncApp.WinForms.csproj
│
├── FileSyncApp.Tests/                   # Unit and integration tests
│   ├── SyncEngineTests.cs
│   ├── ConflictResolverTests.cs
│   └── S3ServiceTests.cs
│
└── docs/
    ├── 01-Architecture.md               # This document
    ├── 02-User-Guide.md                 # End-user documentation
    ├── 03-Deployment.md                 # Installation instructions
    └── 04-API-Reference.md              # Interface documentation
```

---

## Core Design Patterns

### 1. MVP (Model-View-Presenter)

The application uses MVP to decouple UI from business logic:

```csharp
// View Interface - UI contract
public interface IFileSyncView
{
    event EventHandler SyncRequested;
    event EventHandler<string> LocalPathSelected;
    event EventHandler RefreshRequested;
    
    void UpdateLocalTree(List<FileNode> nodes);
    void UpdateRemoteTree(List<FileNode> nodes);
    void ShowProgress(string message, int percentage);
    void ShowConflictDialog(List<ConflictInfo> conflicts);
    string StatusMessage { set; }
}

// Presenter - orchestrates view and services
public class FileSyncPresenter
{
    private readonly IFileSyncView _view;
    private readonly ISyncEngine _syncEngine;
    private readonly IFileStorageService _s3Service;
    
    public FileSyncPresenter(IFileSyncView view, ISyncEngine syncEngine, ...)
    {
        _view = view;
        _view.SyncRequested += OnSyncRequested;
    }
}
```

### 2. Dependency Injection

All services are registered in DI container for testability:

```csharp
// Program.cs
var host = Host.CreateDefaultBuilder()
    .ConfigureServices((context, services) =>
    {
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<IAuthService, CognitoAuthService>();
        services.AddSingleton<IFileStorageService, S3FileStorageService>();
        services.AddSingleton<ISyncEngine, SyncEngine>();
        services.AddTransient<MainForm>();
    })
    .Build();
```

### 3. Repository Pattern for Metadata

SQLite stores sync state for delta detection:

```csharp
public class MetadataCache
{
    private readonly SqliteConnection _connection;
    
    public async Task<FileRecord?> GetLastSyncState(string path);
    public async Task UpdateSyncState(string path, string etag, DateTime modified);
    public async Task<List<FileRecord>> GetAllRecords();
}
```

---

## Key Feature Implementations

### 1. Bidirectional Sync with Conflict Resolution

The sync engine uses three-way comparison:

```
┌──────────────────────────────────────────────────────────────────┐
│                    Sync Decision Matrix                          │
├─────────────┬─────────────┬──────────────────────────────────────┤
│ Local State │Remote State │ Action                               │
├─────────────┼─────────────┼──────────────────────────────────────┤
│ New         │ -           │ Upload to S3                         │
│ -           │ New         │ Download to Local                    │
│ Modified    │ Unchanged   │ Upload to S3                         │
│ Unchanged   │ Modified    │ Download to Local                    │
│ Modified    │ Modified    │ CONFLICT → Apply resolution policy   │
│ Deleted     │ Unchanged   │ Delete from S3                       │
│ Unchanged   │ Deleted     │ Delete from Local                    │
│ Deleted     │ Modified    │ CONFLICT → Prompt user               │
└─────────────┴─────────────┴──────────────────────────────────────┘
```

### 2. Conflict Resolution Strategies

```csharp
public enum ConflictPolicy
{
    NewerWins,        // Compare timestamps, newer version wins
    LocalWins,        // Always prefer local version
    RemoteWins,       // Always prefer S3 version
    KeepBoth,         // Rename with conflict suffix
    PromptUser        // Show dialog for manual resolution
}
```

### 3. Delta Sync Algorithm

```
1. Load last sync snapshot from SQLite
2. List local files (hash + mtime)
3. List S3 objects (ETag + LastModified)
4. Compare each file against snapshot:
   - If local changed since snapshot → candidate for upload
   - If remote changed since snapshot → candidate for download
   - If both changed → conflict
5. Execute transfers with progress tracking
6. Update snapshot in SQLite
```

### 4. Async UI Pattern

All TreeView loading is async with lazy expansion:

```csharp
private async void TreeView_BeforeExpand(object sender, TreeViewCancelEventArgs e)
{
    if (e.Node.Tag is LazyLoadMarker)
    {
        e.Cancel = true;  // Prevent default expansion
        await LoadChildrenAsync(e.Node);
        e.Node.Expand();  // Re-expand after loading
    }
}
```

---

## Data Flow Diagrams

### Sync Operation Flow

```
User clicks "Sync"
        │
        ▼
┌───────────────────┐
│ Presenter receives│
│ SyncRequested     │
└─────────┬─────────┘
          │
          ▼
┌───────────────────┐
│ SyncEngine.       │
│ CalculateChanges()│
└─────────┬─────────┘
          │
    ┌─────┴─────┐
    │           │
    ▼           ▼
┌───────┐  ┌────────┐
│ Local │  │  S3    │
│ Scan  │  │  List  │
└───┬───┘  └────┬───┘
    │           │
    └─────┬─────┘
          │
          ▼
┌───────────────────┐
│ Compare with      │
│ SQLite snapshot   │
└─────────┬─────────┘
          │
    ┌─────┴─────┐
    │ Conflicts?│
    └─────┬─────┘
      Yes │  No
    ┌─────┴─────┐
    ▼           ▼
┌────────┐  ┌──────────┐
│Conflict│  │ Execute  │
│Dialog  │  │ Transfers│
└────┬───┘  └────┬─────┘
     │           │
     └─────┬─────┘
           ▼
   ┌───────────────┐
   │Update SQLite  │
   │snapshot       │
   └───────────────┘
```

### Download with ZIP Support

```
User selects files/folders
        │
        ▼
┌───────────────────────┐
│ Single file selected? │
└───────────┬───────────┘
        Yes │  No (multiple/folder)
    ┌───────┴───────┐
    ▼               ▼
┌────────┐   ┌─────────────┐
│Direct  │   │Create temp  │
│Download│   │ZIP archive  │
└────────┘   └──────┬──────┘
                    │
                    ▼
            ┌───────────────┐
            │Stream objects │
            │into ZIP       │
            └───────┬───────┘
                    │
                    ▼
            ┌───────────────┐
            │Download ZIP   │
            │with progress  │
            └───────────────┘
```

---

## Security Architecture

### Credential Storage

```
┌─────────────────────────────────────────┐
│           Windows Credential Manager     │
│  ┌─────────────────────────────────┐    │
│  │ FileSyncApp-CognitoRefreshToken │    │
│  │ (DPAPI encrypted)               │    │
│  └─────────────────────────────────┘    │
└─────────────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────┐
│         CognitoAuthService               │
│  • Exchanges refresh token for session  │
│  • Obtains temporary AWS credentials    │
│  • 1-hour credential lifetime           │
└─────────────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────┐
│            AWS S3 Access                 │
│  • Uses STS temporary credentials       │
│  • Role-based bucket policies           │
│  • Server-side encryption (SSE-S3)      │
└─────────────────────────────────────────┘
```

### Role-Based Access Control

```
┌────────────────┬──────────────────────────────────────┐
│ Role           │ Permissions                          │
├────────────────┼──────────────────────────────────────┤
│ Administrator  │ Full access to all files/folders     │
│                │ Can manage user permissions          │
│                │ Can delete any file                  │
├────────────────┼──────────────────────────────────────┤
│ Executive      │ Read/write to Executive + User files │
│                │ Cannot modify Admin-only files       │
├────────────────┼──────────────────────────────────────┤
│ User           │ Read/write to User-level files only  │
│                │ Read-only for Executive files        │
└────────────────┴──────────────────────────────────────┘
```

---

## Performance Optimizations

### 1. Parallel S3 Operations

```csharp
// Parallel listing with semaphore throttling
var semaphore = new SemaphoreSlim(10);
var tasks = prefixes.Select(async prefix =>
{
    await semaphore.WaitAsync();
    try { return await ListObjectsAsync(prefix); }
    finally { semaphore.Release(); }
});
```

### 2. Lazy TreeView Loading

- Only load immediate children on expand
- Maximum 500 items per expansion
- Use placeholder "Loading..." nodes

### 3. Delta Sync with ETag/Hash Caching

- Store file hashes in SQLite
- Compare ETag from S3 (already MD5 for non-multipart)
- Skip unchanged files entirely

### 4. Bandwidth Throttling

```csharp
public class ThrottledStream : Stream
{
    private readonly TokenBucket _bucket;
    
    public override async Task<int> ReadAsync(...)
    {
        await _bucket.WaitForTokensAsync(count);
        return await _baseStream.ReadAsync(...);
    }
}
```

---

## Technology Stack

| Component | Technology | Version |
|-----------|------------|---------|
| Runtime | .NET 8.0 Windows | 8.0+ |
| UI Framework | Windows Forms | - |
| UI Toolkit | Krypton Toolkit | 100.x |
| AWS SDK | AWSSDK.S3, AWSSDK.CognitoIdentityProvider | Latest |
| Database | Microsoft.Data.Sqlite | 8.0+ |
| DI Container | Microsoft.Extensions.DependencyInjection | 8.0+ |
| Logging | Serilog | 3.x |
| Testing | xUnit, Moq | Latest |

---

## Deployment Model

### Single Executable Distribution

```
FileSyncApp/
├── FileSyncApp.exe           # Self-contained .NET 8 executable
├── appsettings.json          # Configuration (AWS region, bucket)
├── FileSyncApp.db            # SQLite metadata cache (auto-created)
└── logs/                     # Serilog file logs
    └── filesync-YYYYMMDD.log
```

### Configuration

```json
{
  "AWS": {
    "Region": "ap-southeast-2",
    "BucketName": "eastgate-documents",
    "AccessKey": "",           // Optional: use Cognito instead
    "SecretKey": ""
  },
  "Cognito": {
    "UserPoolId": "ap-southeast-2_xxxxx",
    "ClientId": "xxxxxxxxxxxxxxxxx",
    "IdentityPoolId": "ap-southeast-2:xxxx-xxxx-xxxx"
  },
  "Sync": {
    "ConflictPolicy": "NewerWins",
    "MaxConcurrentTransfers": 5,
    "BandwidthLimitKBps": 0,   // 0 = unlimited
    "ExcludePatterns": ["**/Thumbs.db", "**/.DS_Store", "**/node_modules/**"]
  },
  "Performance": {
    "MaxConcurrentUploads": 5,
    "MaxBytesPerSecond": 0
  }
}
```

---

## Future Enhancements

### Phase 2 (Planned)
- Scheduled sync with Quartz.NET
- FileSystemWatcher for real-time sync
- S3 versioning integration
- Client-side encryption (AES-256)

### Phase 3 (Roadmap)
- Multi-bucket workspace support
- Active Directory integration
- Mobile companion app
- Audit dashboard with search

---

*Document Version: 1.0*  
*Last Updated: February 2026*

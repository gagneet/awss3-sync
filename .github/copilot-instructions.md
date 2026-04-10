# Copilot Instructions

## Build & Test Commands

```powershell
# Restore, build, test
dotnet restore
dotnet build FileSyncApp.sln
dotnet test FileSyncApp.sln

# Run a single test
dotnet test FileSyncApp.Tests/FileSyncApp.Tests.csproj --filter "FullyQualifiedName~SyncEngineTests.ResolveBidirectional_NoChanges_ReturnsSkip"

# Run the app
dotnet run --project FileSyncApp.WinForms/FileSyncApp.WinForms.csproj

# Build and publish (PowerShell script)
.\build.ps1 -Configuration Release
.\build.ps1 -Configuration Release -SelfContained -SingleFile   # standalone exe
```

## Architecture

Four projects targeting .NET 8.0:

- **FileSyncApp.Core** (`net8.0`) — interfaces, models, and all business logic. Has no dependency on AWS or UI.
- **FileSyncApp.S3** (`net8.0`) — AWS SDK implementations of `IFileStorageService` and `IAuthService` (Cognito). References Core only.
- **FileSyncApp.WinForms** (`net8.0-windows`) — Windows Forms MVP UI; owns the DI container (`Program.cs`) and `appsettings.json`. References Core + S3.
- **FileSyncApp.Tests** (`net8.0`) — xUnit + Moq tests for Core logic. References Core + S3.

### Sync Engine — 3-State Algorithm

The `SyncEngine` determines what to do with each file by comparing **three states**:

| State | Source |
|---|---|
| Local | Filesystem scan |
| Remote | S3 listing (`IFileStorageService.ListFilesAsync`) |
| Snapshot/Baseline | SQLite via `MetadataCache` (`FileRecords` table) |

Decision logic in `SyncEngine.DetermineAction`: if only local changed → Upload; only remote changed → Download; both changed → Conflict; local deleted + remote unchanged → DeleteRemote; remote deleted + local unchanged → DeleteLocal.

`MetadataCache` (in Core) is the authoritative sync-state store. It has three SQLite tables: `FileRecords` (per-file snapshot), `SyncHistory` (audit log), and `Settings` (key/value). `DatabaseService` (also in Core) uses a simpler legacy `SyncSnapshot` table and is used separately.

### Authentication

`UnifiedAuthService` wraps `CognitoAuthService` and `LocalAuthService` and delegates based on its `AuthMode` (`Cognito` | `Local`). Both implement `IAuthService`. Registered as a singleton in DI with the concrete services injected into it.

Credentials are persisted via Windows Credential Manager (`Meziantou.Framework.Win32.CredentialManager`) and DPAPI.

### Dependency Injection

All services registered as singletons in `Program.cs` using `Microsoft.Extensions.Hosting`. The DI root is in `FileSyncApp.WinForms`. `MainForm` and `LoginForm` are transient. `FileSyncPresenter` is constructed manually (not resolved from container) to accept the `IFileSyncView` interface.

### Logging

Serilog with `SensitiveDataEnricherOptions` for automatic secret masking. Logs written to `logs/app.log` with daily rolling. Always use structured logging (`_logger.LogInformation("... {Key}", value)`) rather than string interpolation.

## Key Conventions

**Unified file model:** `FileNode` is the canonical model for both local and S3 files. Use `IsS3` to distinguish. `LocalFileItem` and `S3FileItem` are legacy types still present in Models but not used by the sync engine.

**Interfaces first:** All cross-project dependencies go through `FileSyncApp.Core.Interfaces`. Never reference concrete service types across project boundaries (only Core and WinForms wire up concrete types).

**Async everywhere:** All I/O operations are `async Task<T>`. `CancellationToken` is threaded through all public async methods. `IProgress<SyncProgress>` is passed optionally for UI reporting.

**Access roles on files:** `FileNode.AccessRoles` (`List<UserRole>`) controls which roles can see/access a file. `IFileStorageService.ListFilesAsync` takes a `UserRole` to filter results. `UserRole` enum: `Administrator`, `Executive`, `User`.

**Conflict resolution:** `ConflictPolicy` enum drives automated resolution; `PromptUser` triggers the `ConflictsDetected` event and opens `ConflictForm`. Handle this event in the presenter before calling `ExecuteSyncAsync`.

**Performance config:** Parallelism, chunk sizes, throttling, and delta sync are all controlled by `PerformanceConfig` in `appsettings.json` → `AppConfig.Performance`. Default chunk size is 5 MB; `MaxBytesPerSecond = 0` means unlimited.

**Scheduled sync:** `SyncJob` (Core) implements `Quartz.IJob` for background scheduling. Polly is available in Core for retry policies on transient S3 errors.

**Tests:** xUnit with `[Fact]` (no `[Theory]` currently). Moq for all interface mocks. `SyncEngine.ResolveBidirectional` is public and directly testable without async infrastructure.

## Configuration

`FileSyncApp.WinForms/appsettings.json` is the only config file and is always copied to output. Secrets should never be committed — use environment-specific overrides or Windows Credential Manager at runtime.

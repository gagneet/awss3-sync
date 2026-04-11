# AWS S3 Sync — FileSyncApp

A Windows desktop application for bidirectional synchronisation between local directories and AWS S3 buckets. Built on .NET 8 WinForms with a modern dual-pane UI.

---

## ✨ Features

| Feature | Details |
|---|---|
| **Dual-pane file browser** | Local filesystem on the left, S3 bucket on the right |
| **Smart Upload** | Recursively expands selected folders; only uploads files that are new or changed (size + timestamp comparison) |
| **Smart Download** | Recursively expands S3 folders; skips files already present and identical locally |
| **Select All / Ctrl+A** | Checkbox-toggle on both panes; smart: checks all if any unchecked, unchecks all if all checked |
| **Bidirectional Sync** | 3-state sync engine (local / remote / SQLite snapshot) via **Sync Now** button |
| **Conflict resolution** | Interactive dialog for detected file conflicts |
| **AWS Cognito auth** | Sign in with Cognito User Pools / Identity Pools |
| **Local auth** | Offline fallback with admin / exec / user accounts |
| **Standalone executable** | Single-file self-contained `.exe` — no installer or .NET runtime required |
| **High-DPI / Win11 ready** | `PerMonitorV2` DPI awareness, Windows 11 compatibility manifest |

---

## 🗂 Project Structure

```
FileSyncApp.sln
├── FileSyncApp.Core/        # Business logic, sync engine, models, interfaces
├── FileSyncApp.S3/          # AWS S3 + Cognito service implementations
├── FileSyncApp.WinForms/    # UI (dual-pane ListView, forms, DI wiring)
└── FileSyncApp.Tests/       # Unit tests (xUnit + Moq)
```

---

## 🚀 Quick Start (Development)

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- Visual Studio 2022 with **.NET Desktop Development** workload
- An AWS account with an S3 bucket

### Run from Visual Studio
1. Open `FileSyncApp.sln`
2. Set `FileSyncApp.WinForms` as the startup project
3. Copy `FileSyncApp.WinForms/appsettings.template.json` → `appsettings.json` and fill in your AWS details
4. Press **F5**

### Run from CLI
```powershell
dotnet restore
dotnet run --project FileSyncApp.WinForms/FileSyncApp.WinForms.csproj
```

---

## ⚙️ Configuration

Copy `FileSyncApp.WinForms/appsettings.template.json` to `FileSyncApp.WinForms/appsettings.json` (gitignored) and set your values:

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
    "ClientId": "xxxxxxxxxxxxxxxxxxxxxxxx",
    "IdentityPoolId": "us-east-1:xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
  },
  "Performance": {
    "MaxConcurrentUploads": 5,
    "MaxBytesPerSecond": 0
  }
}
```

> `appsettings.json` is listed in `.gitignore` — your credentials will never be committed.

---

## 🔐 Authentication

### AWS Cognito *(recommended)*
Uses Cognito User Pools. Requires the `Cognito` section in `appsettings.json`.

### Local Login *(fallback / offline)*
Built-in accounts for testing without Cognito:

| Username | Password | Role |
|---|---|---|
| `admin` | `admin` | Administrator |
| `exec` | `exec` | Executive |
| `user` | `user` | User |

---

## 📦 Building a Standalone Executable

Use the included `build.ps1` script:

```powershell
# Default: self-contained single-file win-x64 Release, runs tests, zips output
.\build.ps1

# Options
.\build.ps1 -Version 2.0.0
.\build.ps1 -Runtime win-x86
.\build.ps1 -NoSingleFile    # framework-dependent, smaller output
.\build.ps1 -SkipTests
```

Output: `publish/FileSyncApp-<version>-<runtime>.zip`

The zip contains a single `.exe` with no external dependencies — copy it anywhere on a Windows 10/11 machine and run.

---

## 🏷️ Creating a Release

Tag a commit with a semantic version to trigger the GitHub Actions release workflow:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

The workflow builds a self-contained single-file `.exe`, zips it, and creates a GitHub Release with the zip attached.

---

## Publishing a release as a standalone executable

● Use the repo’s built-in publish script:

.\build.ps1

That creates a self-contained single-file Windows build. The target laptop does not need Visual Studio, the source code, or the project DLLs.

1. From the repo root, run: .\build.ps1 -Version 1.0.0
2. Take the generated output:
 - publish\FileSyncApp.exe
 - the packaged zip FileSyncApp-1.0.0-win-x64.zip
3. Copy the zip to the laptop, extract it, edit appsettings.json, then run FileSyncApp.exe.

Important: if you want no DLLs, do not use -NoSingleFile.
The app itself is a single .exe, but you should still keep appsettings.json beside it for configuration.

Useful variants:

.\build.ps1 -Runtime win-x64      # normal Intel/AMD laptop
.\build.ps1 -Runtime win-arm64    # ARM Windows laptop
.\build.ps1 -SkipTests            # faster packaging

If you want, the equivalent raw .NET command is:

dotnet publish FileSyncApp.WinForms\FileSyncApp.WinForms.csproj -c Release -r win-x64 --self-contained true -o publish -p:PublishSingleFile=true

---

## 🧪 Running Tests

```powershell
dotnet test FileSyncApp.sln
```

---

## 🛠 Troubleshooting

| Problem | Fix |
|---|---|
| "Metadata file could not be found" | **Clean Solution** → **Rebuild Solution** in Visual Studio |
| Splitter layout error on login | Ensure display scale is not set to an unusual value; the form initialises the splitter after layout |
| S3 listing hangs | Check AWS credentials and network; the app times out after 60 s |
| Login fails with Cognito | Verify `UserPoolId`, `ClientId`, and `Region`; use Local Login as fallback |

---

## 📜 License

[MIT](LICENSE)

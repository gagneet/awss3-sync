\# AWS S3 Sync Utility - Developer Guide



This guide helps contributors set up, work on, and extend the project. It also covers coding styles and open development gaps.



---



\## Table of Contents



1\. Project Setup

2\. Architecture Overview

3\. Coding Guidelines

4\. Contribution Workflow

5\. Known Gaps \& TODOs

6\. Resources



---



\## 1. Project Setup



\- Requires Visual Studio 2022, .NET 8.0 SDK.

\- Clone repo, open `AWSS3Sync.sln`, restore NuGet packages.

\- Create `appsettings.json` for local testing.

\- Build and run (`F5`).



---



\## 2. Architecture Overview



\- \*\*Models:\*\* UserRole, FileItem, AppConfig

\- \*\*Services:\*\* S3Service, FileService, MetadataService, ConfigurationService

\- \*\*Forms:\*\* LoginForm, MainForm, RoleSelectionForm, ProgressForm



See README for file structure.



---



\## 3. Coding Guidelines



\### C#/.NET/WinForms Best Practices



\- Use \[PascalCase](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions) for public members, \[camelCase](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions) for private fields.

\- Prefer `async`/`await` for IO/network operations.

\- Keep UI logic separated from business logic. Use service classes for AWS/local operations.

\- Use proper error handling (`try/catch`) and logging.

\- Dispose resources (files, streams, network) properly.

\- Use \[Windows Forms designer](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/) for layout, avoid manual UI code when possible.

\- Follow SOLID principles for maintainable code.

\- Document public methods/classes with XML comments.

\- Write unit tests for core logic (see `SyncFeatureTestPlan.md`).



\### Workflow



\- Create feature branches per change/issue.

\- Use clear, descriptive commit messages.

\- Open pull requests for code review.



---



\## 4. Contribution Workflow



1\. Fork and clone the repo.

2\. Create a new branch: `git checkout -b feature/your-feature`

3\. Make changes, commit, and push.

4\. Open a PR—describe your changes, link related issues.

5\. Address code review feedback and update your branch.



---



\## 5. Known Gaps \& TODOs



\- \*\*Sync feature:\*\* Not fully implemented.

\- \*\*Unit/Integration tests:\*\* Limited or missing.

\- \*\*Permission UI:\*\* May need more granularity and bulk editing features.

\- \*\*Audit logging:\*\* Not present.

\- \*\*Multi-factor authentication (MFA):\*\* Not implemented.

\- \*\*Error reporting:\*\* Needs improvement (user-friendly messages, logging).

\- \*\*Documentation:\*\* More screenshots, flow diagrams, and dev onboarding steps.



---



\## 6. Resources



\- \[.NET Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)

\- \[WinForms Best Practices](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/)

\- \[AWS SDK for .NET](https://docs.aws.amazon.com/sdk-for-net/)

\- \[Contributing to Open Source](https://opensource.guide/how-to-contribute/)




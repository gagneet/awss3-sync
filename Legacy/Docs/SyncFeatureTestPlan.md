# Test Plan: S3 to Local Sync Functionality

This document outlines steps and checks for validating the sync feature between local files and AWS S3.

---

## Test Areas

1. **Configuration**
   - [ ] Correct AWS credentials
   - [ ] Accessible S3 bucket

2. **UI**
   - [ ] Sync button visible and enabled for supported roles
   - [ ] Proper error messages for failed sync

3. **Functional**
   - [ ] Local → S3 sync uploads all selected files/folders
   - [ ] S3 → Local sync downloads all selected files/folders
   - [ ] File/folder structure preserved after sync
   - [ ] Metadata and permissions/tags remain intact

4. **Edge Cases**
   - [ ] Conflict resolution (same file exists in both locations)
   - [ ] Network failure/retry handling
   - [ ] Large files or deep directories handled gracefully
   - [ ] Multi-user access scenarios

5. **Security**
   - [ ] Only authorized users can sync
   - [ ] Permission checks for uploads/downloads

6. **Logging**
   - [ ] Sync actions are logged for audit

7. **Performance**
   - [ ] Sync time for large sets within acceptable limits

---

## Test Steps

1. Configure app and log in as different roles.
2. Attempt sync operations—verify results in AWS S3 and local folders.
3. Check logs, UI messages, and error handling.
4. Review permission/tagging after sync.

---

## Reporting

Document all findings, failures, and screenshots for issues.

**SUT (Software Under Test):**
*   `SyncS3ToLocal` method in `Forms/MainForm.Operations.cs`
*   `SyncButton_Click` event handler in `Forms/MainForm.Events.cs`

**General Test Assumptions:**
*   The `_s3Service.DownloadFileAsync` method correctly downloads files from S3 and overwrites local files if they exist in the target download directory.
*   The `_s3Files` list accurately reflects the state of the S3 bucket accessible to the current user role before the sync operation begins.
*   Local file paths and S3 keys are correctly transformed for comparison (e.g., handling path separators `/` vs `\`).
*   File timestamps are compared in UTC.
*   The `ProgressForm` displays messages but doesn't interfere with the core logic.
*   The "Sync Folder" button is only enabled and clicked when a local path is selected and the user is an Administrator. The "S3 to Local" direction is chosen in the `SyncDirectionForm`.

---

## Test Scenarios

**Scenario 1: Empty Local Folder, S3 has files**
*   **Setup:**
    *   Local folder (`localPath`): Empty.
    *   S3 bucket (`_s3Files`):
        *   `file1.txt` (Key: "file1.txt", Size: 1KB, LastModified: 2023-01-01)
        *   `folderA/fileA1.txt` (Key: "folderA/fileA1.txt", Size: 2KB, LastModified: 2023-01-01)
*   **Action:** User clicks "Sync Folder" (choosing S3 to Local).
*   **Expected Outcome:**
    *   Files downloaded to local folder:
        *   `localPath/file1.txt`
        *   `localPath/folderA/fileA1.txt` (directory `folderA` created)
    *   Files remaining in local folder: None (initially empty).
    *   Warning messages: None. A success message like "Sync completed successfully! No extra local files found." should be displayed.

**Scenario 2: Local Folder Matches S3**
*   **Setup:**
    *   Local folder (`localPath`):
        *   `file1.txt` (Size: 1KB, LastWriteTimeUtc: 2023-01-01)
        *   `folderA/fileA1.txt` (Size: 2KB, LastWriteTimeUtc: 2023-01-01)
    *   S3 bucket (`_s3Files`):
        *   `file1.txt` (Key: "file1.txt", Size: 1KB, LastModified: 2023-01-01)
        *   `folderA/fileA1.txt` (Key: "folderA/fileA1.txt", Size: 2KB, LastModified: 2023-01-01)
*   **Action:** Click "Sync Folder" (choosing S3 to Local).
*   **Expected Outcome:**
    *   Files downloaded: None.
    *   Files remaining in local folder:
        *   `localPath/file1.txt`
        *   `localPath/folderA/fileA1.txt`
    *   Warning messages: None. Success message displayed.

**Scenario 3: Local Folder Has Extra Files**
*   **Setup:**
    *   Local folder (`localPath`):
        *   `file1.txt` (Size: 1KB, LastWriteTimeUtc: 2023-01-01)
        *   `extra.txt` (Size: 500B, LastWriteTimeUtc: 2023-01-05)
        *   `folderA/fileA1.txt` (Size: 2KB, LastWriteTimeUtc: 2023-01-01)
    *   S3 bucket (`_s3Files`):
        *   `file1.txt` (Key: "file1.txt", Size: 1KB, LastModified: 2023-01-01)
        *   `folderA/fileA1.txt` (Key: "folderA/fileA1.txt", Size: 2KB, LastModified: 2023-01-01)
*   **Action:** Click "Sync Folder" (choosing S3 to Local).
*   **Expected Outcome:**
    *   Files downloaded: None.
    *   Files remaining in local folder:
        *   `localPath/file1.txt`
        *   `localPath/extra.txt`
        *   `localPath/folderA/fileA1.txt`
    *   Warning messages: A warning message displayed, listing `localPath\extra.txt`.

**Scenario 4: S3 Has New Files**
*   **Setup:**
    *   Local folder (`localPath`):
        *   `file1.txt` (Size: 1KB, LastWriteTimeUtc: 2023-01-01)
    *   S3 bucket (`_s3Files`):
        *   `file1.txt` (Key: "file1.txt", Size: 1KB, LastModified: 2023-01-01)
        *   `newFile.txt` (Key: "newFile.txt", Size: 3KB, LastModified: 2023-01-02)
        *   `folderB/fileB1.txt` (Key: "folderB/fileB1.txt", Size: 4KB, LastModified: 2023-01-03)
*   **Action:** Click "Sync Folder" (choosing S3 to Local).
*   **Expected Outcome:**
    *   Files downloaded to local folder:
        *   `localPath/newFile.txt`
        *   `localPath/folderB/fileB1.txt` (directory `folderB` created)
    *   Files remaining in local folder:
        *   `localPath/file1.txt` (untouched)
        *   `localPath/newFile.txt` (newly downloaded)
        *   `localPath/folderB/fileB1.txt` (newly downloaded)
    *   Warning messages: None. Success message displayed.

**Scenario 5: S3 Files Are Newer / Different Size**
*   **Setup:**
    *   Local folder (`localPath`):
        *   `file1.txt` (Size: 1KB, LastWriteTimeUtc: 2023-01-01) // Older
        *   `folderA/fileA1.txt` (Size: 2KB, LastWriteTimeUtc: 2023-01-01) // Different size
    *   S3 bucket (`_s3Files`):
        *   `file1.txt` (Key: "file1.txt", Size: 1KB, LastModified: 2023-01-02) // Newer
        *   `folderA/fileA1.txt` (Key: "folderA/fileA1.txt", Size: 3KB, LastModified: 2023-01-01) // Larger size
*   **Action:** Click "Sync Folder" (choosing S3 to Local).
*   **Expected Outcome:**
    *   Files downloaded (overwriting local versions):
        *   `localPath/file1.txt` (updated content from S3)
        *   `localPath/folderA/fileA1.txt` (updated content from S3)
    *   Warning messages: None. Success message displayed.

**Scenario 6: Local Files Are Newer Than S3 (No S3 change)**
*   **Setup:**
    *   Local folder (`localPath`):
        *   `file1.txt` (Size: 1KB, LastWriteTimeUtc: 2023-01-02) // Newer than S3
    *   S3 bucket (`_s3Files`):
        *   `file1.txt` (Key: "file1.txt", Size: 1KB, LastModified: 2023-01-01)
*   **Action:** Click "Sync Folder" (choosing S3 to Local).
*   **Expected Outcome:**
    *   Files downloaded: None.
    *   Files remaining in local folder:
        *   `localPath/file1.txt` (NOT overwritten)
    *   Warning messages: None. Success message displayed.

**Scenario 7: Mix of all above (Complex Case)**
*   **Setup:**
    *   Local folder (`localPath`):
        *   `common_old.txt` (Size: 1KB, LastWriteTimeUtc: 2023-01-01)
        *   `common_new_local.txt` (Size: 1KB, LastWriteTimeUtc: 2023-01-03)
        *   `only_local.txt` (Size: 1KB, LastWriteTimeUtc: 2023-01-01)
        *   `existing_folder/local_only_in_folder.txt` (Size: 1KB, LastWriteTimeUtc: 2023-01-01)
    *   S3 bucket (`_s3Files`):
        *   `common_old.txt` (Key: "common_old.txt", Size: 1KB, LastModified: 2023-01-02) // Newer in S3
        *   `common_new_local.txt` (Key: "common_new_local.txt", Size: 1KB, LastModified: 2023-01-01) // Older in S3
        *   `only_s3.txt` (Key: "only_s3.txt", Size: 1KB, LastModified: 2023-01-01)
        *   `new_s3_folder/s3_file_in_new_folder.txt` (Key: "new_s3_folder/s3_file_in_new_folder.txt", Size: 1KB, LastModified: 2023-01-01)
        *   `existing_folder/s3_file_in_existing_folder.txt` (Key: "existing_folder/s3_file_in_existing_folder.txt", Size: 1KB, LastModified: 2023-01-01)
*   **Action:** Click "Sync Folder" (choosing S3 to Local).
*   **Expected Outcome:**
    *   Files downloaded to local folder (or local files overwritten):
        *   `localPath/common_old.txt` (updated from S3)
        *   `localPath/only_s3.txt` (newly downloaded)
        *   `localPath/new_s3_folder/s3_file_in_new_folder.txt` (newly downloaded, `new_s3_folder` created)
        *   `localPath/existing_folder/s3_file_in_existing_folder.txt` (newly downloaded into existing `existing_folder`)
    *   Files remaining in local folder (and their state):
        *   `localPath/common_old.txt` (updated)
        *   `localPath/common_new_local.txt` (remains, not overwritten as local is newer)
        *   `localPath/only_local.txt` (remains, will be in warning)
        *   `localPath/existing_folder/local_only_in_folder.txt` (remains, will be in warning)
        *   `localPath/only_s3.txt` (new)
        *   `localPath/new_s3_folder/s3_file_in_new_folder.txt` (new)
        *   `localPath/existing_folder/s3_file_in_existing_folder.txt` (new)
    *   Warning messages: A warning message listing `localPath\only_local.txt` and `localPath\existing_folder\local_only_in_folder.txt`.

---
## Code Review Confirmation Summary:

Based on a detailed review of the implemented logic in `SyncS3ToLocal` (in `Forms/MainForm.Operations.cs`) and the interaction with `SyncButton_Click` (in `Forms/MainForm.Events.cs`), the code **should correctly handle all the specified test scenarios**. The logic for comparing local and S3 files (existence, timestamps, sizes), deciding whether to download, creating directories, and identifying extra local files aligns with the expected outcomes for each scenario.

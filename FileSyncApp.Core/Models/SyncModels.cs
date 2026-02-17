namespace FileSyncApp.Core.Models;

public enum SyncActionType { Skip, Upload, Download, DeleteLocal, DeleteRemote, Conflict, KeepBoth }

public record SyncActionRequest(string Path, SyncActionType Action, FileNode? Local, FileNode? Remote);

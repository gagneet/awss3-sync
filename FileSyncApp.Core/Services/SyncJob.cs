using FileSyncApp.Core.Interfaces;
using FileSyncApp.Core.Models;
using Quartz;

namespace FileSyncApp.Core.Services;

[DisallowConcurrentExecution]
public class SyncJob : IJob
{
    private readonly ISyncEngine _syncEngine;

    public SyncJob(ISyncEngine syncEngine)
    {
        _syncEngine = syncEngine;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var localPath = context.MergedJobDataMap.GetString("LocalPath") ?? "";
        var remotePrefix = context.MergedJobDataMap.GetString("RemotePrefix") ?? "";
        // For scheduled jobs, we might need a way to store the user role.
        // Assuming Administrator for now if not provided, or better, pass it in data map.
        var userRole = (UserRole)context.MergedJobDataMap.GetInt("UserRole");

        Func<SyncActionRequest, Task<SyncActionType>> conflictResolver = (req) =>
        {
            if (req.Local!.LastModified > req.Remote!.LastModified)
                return Task.FromResult(SyncActionType.Upload);
            else
                return Task.FromResult(SyncActionType.Download);
        };

        await _syncEngine.SyncAsync(localPath, remotePrefix, userRole, new Progress<SyncProgress>(), conflictResolver, context.CancellationToken);
    }
}

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

        await _syncEngine.SyncAsync(localPath, remotePrefix, ConflictPolicy.NewerWins,
            new Progress<SyncProgress>(), context.CancellationToken);
    }
}

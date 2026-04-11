using FileSyncApp.Core.Models;

namespace FileSyncApp.Core.Interfaces;

/// <summary>
/// Manages scheduled background sync jobs using Quartz.NET.
/// </summary>
public interface ISyncSchedulerService
{
    /// <summary>Start the Quartz scheduler and schedule all enabled jobs.</summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Gracefully shut down the scheduler.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>Add a new schedule or update an existing one (matched by Id).</summary>
    Task AddOrUpdateScheduleAsync(SyncSchedule schedule);

    /// <summary>
    /// Remove a schedule's Quartz job and trigger by Id.
    /// The caller is responsible for deleting the schedule from config if desired.
    /// </summary>
    Task RemoveScheduleAsync(string scheduleId);

    /// <summary>Return all configured schedules with refreshed NextRun values.</summary>
    Task<List<SyncSchedule>> GetSchedulesWithNextRunAsync();

    /// <summary>Whether the scheduler is currently running.</summary>
    bool IsRunning { get; }
}

using FileSyncApp.Core.Interfaces;
using FileSyncApp.Core.Models;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl;

namespace FileSyncApp.Core.Services;

/// <summary>
/// Implements <see cref="ISyncSchedulerService"/> using Quartz.NET.
/// Schedules enabled <see cref="SyncSchedule"/> entries as cron-triggered jobs.
/// </summary>
public sealed class SyncSchedulerService : ISyncSchedulerService, IAsyncDisposable
{
    private readonly IConfigurationService _configService;
    private readonly ILogger<SyncSchedulerService> _logger;
    private IScheduler? _scheduler;

    public bool IsRunning => _scheduler?.IsStarted ?? false;

    public SyncSchedulerService(
        IConfigurationService configService,
        ILogger<SyncSchedulerService> logger)
    {
        _configService = configService;
        _logger        = logger;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_scheduler is not null) return;

        var factory = new StdSchedulerFactory();
        _scheduler  = await factory.GetScheduler(cancellationToken);

        // Add a listener that persists LastRun/NextRun to config
        _scheduler.ListenerManager.AddJobListener(
            new SyncJobListener(_configService, _logger));

        await _scheduler.Start(cancellationToken);
        _logger.LogInformation("Quartz scheduler started");

        // Load all enabled schedules from config
        foreach (var s in _configService.GetSchedules().Where(s => s.IsEnabled))
        {
            try { await ScheduleJobAsync(s, cancellationToken); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to schedule '{Name}'", s.Name); }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_scheduler is null) return;
        await _scheduler.Shutdown(waitForJobsToComplete: true, cancellationToken);
        _logger.LogInformation("Quartz scheduler stopped");
        _scheduler = null;
    }

    // ── CRUD ──────────────────────────────────────────────────────────────

    public async Task AddOrUpdateScheduleAsync(SyncSchedule schedule)
    {
        _configService.SaveSchedule(schedule);

        if (_scheduler is null) return;

        var key = JobKey(schedule.Id);
        if (await _scheduler.CheckExists(key))
            await _scheduler.DeleteJob(key);

        if (schedule.IsEnabled)
            await ScheduleJobAsync(schedule);
    }

    public async Task RemoveScheduleAsync(string scheduleId)
    {
        _configService.DeleteSchedule(scheduleId);

        if (_scheduler is null) return;
        var key = JobKey(scheduleId);
        if (await _scheduler.CheckExists(key))
            await _scheduler.DeleteJob(key);
    }

    public async Task<List<SyncSchedule>> GetSchedulesWithNextRunAsync()
    {
        var schedules = _configService.GetSchedules();
        if (_scheduler is null) return schedules;

        foreach (var s in schedules)
        {
            var triggers = await _scheduler.GetTriggersOfJob(JobKey(s.Id));
            var next = triggers.Select(t => t.GetNextFireTimeUtc()?.LocalDateTime)
                               .Where(t => t.HasValue)
                               .Select(t => t!.Value)
                               .OrderBy(t => t)
                               .FirstOrDefault();
            if (next != default) s.NextRun = next;
        }
        return schedules;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static JobKey JobKey(string id) => new JobKey(id, "sync");

    private async Task ScheduleJobAsync(SyncSchedule s, CancellationToken ct = default)
    {
        if (_scheduler is null) return;

        var job = JobBuilder.Create<SyncJob>()
            .WithIdentity(JobKey(s.Id))
            .UsingJobData("LocalPath",    s.LocalPath)
            .UsingJobData("RemotePrefix", s.RemotePrefix)
            .UsingJobData("ScheduleId",   s.Id)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity(s.Id, "sync-trigger")
            .WithCronSchedule(s.CronExpression)
            .Build();

        await _scheduler.ScheduleJob(job, trigger, ct);

        var nextFire = trigger.GetNextFireTimeUtc()?.LocalDateTime;
        if (nextFire.HasValue)
        {
            s.NextRun = nextFire;
            _configService.SaveSchedule(s);
        }

        _logger.LogInformation("Scheduled '{Name}' cron={Cron} next={Next}",
            s.Name, s.CronExpression, nextFire?.ToString("g") ?? "n/a");
    }

    public async ValueTask DisposeAsync()
    {
        if (_scheduler is not null)
            await StopAsync();
    }
}

// ── Job listener ─────────────────────────────────────────────────────────

file sealed class SyncJobListener : IJobListener
{
    private readonly IConfigurationService _configService;
    private readonly ILogger _logger;

    public string Name => "SyncJobListener";

    public SyncJobListener(IConfigurationService configService, ILogger logger)
    {
        _configService = configService;
        _logger        = logger;
    }

    public Task JobToBeExecuted(IJobExecutionContext context, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task JobExecutionVetoed(IJobExecutionContext context, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task JobWasExecuted(IJobExecutionContext context, JobExecutionException? jobException, CancellationToken ct = default)
    {
        var scheduleId = context.MergedJobDataMap.GetString("ScheduleId");
        if (string.IsNullOrEmpty(scheduleId)) return Task.CompletedTask;

        var schedules = _configService.GetSchedules();
        var s = schedules.FirstOrDefault(x => x.Id == scheduleId);
        if (s is null) return Task.CompletedTask;

        s.LastRun = DateTime.Now;
        s.NextRun = context.NextFireTimeUtc?.LocalDateTime;
        _configService.SaveSchedule(s);

        if (jobException is not null)
            _logger.LogError(jobException, "Scheduled sync '{Name}' failed", s.Name);
        else
            _logger.LogInformation("Scheduled sync '{Name}' completed. Next: {Next}",
                s.Name, s.NextRun?.ToString("g") ?? "n/a");

        return Task.CompletedTask;
    }
}

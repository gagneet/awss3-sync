using FileSyncApp.Core.Models;

namespace FileSyncApp.Core.Interfaces;

public interface IConfigurationService
{
    AppConfig GetConfiguration();

    // ── Profile management ────────────────────────────────────────────────────

    /// <summary>All named bucket profiles (never null).</summary>
    List<BucketProfile> GetProfiles();

    /// <summary>The currently active profile, or null if using the legacy AWS block.</summary>
    BucketProfile? GetActiveProfile();

    /// <summary>Set the active profile by name and persist the change.</summary>
    void SetActiveProfile(string profileName);

    /// <summary>Add or update a profile, then persist.</summary>
    void SaveProfile(BucketProfile profile);

    /// <summary>Remove a profile by name and persist.</summary>
    void DeleteProfile(string profileName);

    // ── Schedule management ───────────────────────────────────────────────────

    /// <summary>All configured sync schedules (never null).</summary>
    List<SyncSchedule> GetSchedules();

    /// <summary>Add or update a schedule, then persist.</summary>
    void SaveSchedule(SyncSchedule schedule);

    /// <summary>Remove a schedule by Id, then persist.</summary>
    void DeleteSchedule(string scheduleId);

    /// <summary>Persist the entire configuration to appsettings.json.</summary>
    void Save();
}

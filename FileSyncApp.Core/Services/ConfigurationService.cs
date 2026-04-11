using FileSyncApp.Core.Interfaces;
using FileSyncApp.Core.Models;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Diagnostics;

namespace FileSyncApp.Core.Services;

public class ConfigurationService : IConfigurationService
{
    private readonly IConfiguration _configuration;
    private AppConfig? _config;

    private static string SettingsPath =>
        Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    public ConfigurationService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public AppConfig GetConfiguration()
    {
        if (_config == null)
        {
            _config = new AppConfig();

            // 1. Try to bind from injected IConfiguration (default host behavior)
            try
            {
                _configuration.Bind(_config);
                Debug.WriteLine("Configuration bound from IConfiguration.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to bind from IConfiguration: {ex.Message}");
            }

            // 2. If binding failed or BucketName is empty, try manual load from common locations
            if (string.IsNullOrEmpty(_config.AWS.BucketName))
            {
                var possiblePaths = new[]
                {
                    SettingsPath,
                    Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"),
                    "appsettings.json"
                };

                foreach (var path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        try
                        {
                            Debug.WriteLine($"Attempting manual load from {path}...");
                            string json = File.ReadAllText(path);
                            var manualConfig = JsonConvert.DeserializeObject<AppConfig>(json);
                            if (manualConfig != null && !string.IsNullOrEmpty(manualConfig.AWS.BucketName))
                            {
                                _config = manualConfig;
                                Debug.WriteLine($"Configuration manually loaded from {path}.");
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Failed to manually load config from {path}: {ex.Message}");
                        }
                    }
                }
            }

            // 3. Last resort: Try explicit key lookup
            if (string.IsNullOrEmpty(_config.AWS.BucketName))
            {
                _config.AWS.AccessKey  = _configuration["AWS:AccessKey"]  ?? _config.AWS.AccessKey;
                _config.AWS.SecretKey  = _configuration["AWS:SecretKey"]  ?? _config.AWS.SecretKey;
                _config.AWS.Region     = _configuration["AWS:Region"]     ?? _config.AWS.Region;
                _config.AWS.BucketName = _configuration["AWS:BucketName"] ?? _config.AWS.BucketName;
            }

            // 4. Migrate legacy AWS block → default profile if no profiles exist
            if (_config.Profiles.Count == 0 && !string.IsNullOrEmpty(_config.AWS.BucketName))
            {
                _config.Profiles.Add(new BucketProfile
                {
                    Name       = "Default",
                    AccessKey  = _config.AWS.AccessKey,
                    SecretKey  = _config.AWS.SecretKey,
                    Region     = _config.AWS.Region,
                    BucketName = _config.AWS.BucketName
                });
                _config.ActiveProfileName = "Default";
            }
        }
        return _config;
    }

    // ── Profile management ────────────────────────────────────────────────────

    public List<BucketProfile> GetProfiles() => GetConfiguration().Profiles;

    public BucketProfile? GetActiveProfile()
    {
        var cfg = GetConfiguration();
        if (string.IsNullOrEmpty(cfg.ActiveProfileName))
            return cfg.Profiles.FirstOrDefault();
        return cfg.Profiles.FirstOrDefault(p =>
            string.Equals(p.Name, cfg.ActiveProfileName, StringComparison.OrdinalIgnoreCase));
    }

    public void SetActiveProfile(string profileName)
    {
        var cfg = GetConfiguration();
        cfg.ActiveProfileName = profileName;

        // Keep legacy AWS block in sync so old code paths still work
        var profile = cfg.Profiles.FirstOrDefault(p =>
            string.Equals(p.Name, profileName, StringComparison.OrdinalIgnoreCase));
        if (profile != null)
        {
            cfg.AWS.AccessKey  = profile.AccessKey;
            cfg.AWS.SecretKey  = profile.SecretKey;
            cfg.AWS.Region     = profile.Region;
            cfg.AWS.BucketName = profile.BucketName;
        }

        Save();
    }

    public void SaveProfile(BucketProfile profile)
    {
        var cfg = GetConfiguration();
        var existing = cfg.Profiles.FindIndex(p =>
            string.Equals(p.Name, profile.Name, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0)
            cfg.Profiles[existing] = profile;
        else
            cfg.Profiles.Add(profile);

        // If this is the active profile, keep AWS block in sync
        if (string.Equals(cfg.ActiveProfileName, profile.Name, StringComparison.OrdinalIgnoreCase))
        {
            cfg.AWS.AccessKey  = profile.AccessKey;
            cfg.AWS.SecretKey  = profile.SecretKey;
            cfg.AWS.Region     = profile.Region;
            cfg.AWS.BucketName = profile.BucketName;
        }

        Save();
    }

    public void DeleteProfile(string profileName)
    {
        var cfg = GetConfiguration();
        cfg.Profiles.RemoveAll(p =>
            string.Equals(p.Name, profileName, StringComparison.OrdinalIgnoreCase));

        if (string.Equals(cfg.ActiveProfileName, profileName, StringComparison.OrdinalIgnoreCase))
            cfg.ActiveProfileName = cfg.Profiles.FirstOrDefault()?.Name ?? string.Empty;

        Save();
    }

    // ── Schedule management ───────────────────────────────────────────────────

    public List<SyncSchedule> GetSchedules() => GetConfiguration().Schedules;

    public void SaveSchedule(SyncSchedule schedule)
    {
        var cfg = GetConfiguration();
        var idx = cfg.Schedules.FindIndex(s => s.Id == schedule.Id);
        if (idx >= 0) cfg.Schedules[idx] = schedule;
        else cfg.Schedules.Add(schedule);
        Save();
    }

    public void DeleteSchedule(string scheduleId)
    {
        var cfg = GetConfiguration();
        cfg.Schedules.RemoveAll(s => s.Id == scheduleId);
        Save();
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    public void Save()
    {
        try
        {
            var cfg = GetConfiguration();
            File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(cfg, Formatting.Indented));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save configuration: {ex.Message}");
        }
    }
}

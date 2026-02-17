using FileSyncApp.Core.Interfaces;
using FileSyncApp.Core.Models;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace FileSyncApp.Core.Services;

public class ConfigurationService : IConfigurationService
{
    private readonly IConfiguration _configuration;
    private AppConfig? _config;

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
            _configuration.Bind(_config);

            // 2. If binding failed (e.g. appsettings.json not found by host), try manual load
            if (string.IsNullOrEmpty(_config.AWS.BucketName))
            {
                string exeDir = AppContext.BaseDirectory;
                string configPath = Path.Combine(exeDir, "appsettings.json");

                if (File.Exists(configPath))
                {
                    try
                    {
                        string json = File.ReadAllText(configPath);
                        var manualConfig = JsonConvert.DeserializeObject<AppConfig>(json);
                        if (manualConfig != null)
                        {
                            _config = manualConfig;
                        }
                    }
                    catch { /* Fallback */ }
                }
            }

            // 3. Last resort: Try explicit key lookup for "AWS:BucketName" etc.
            if (string.IsNullOrEmpty(_config.AWS.BucketName))
            {
                _config.AWS.AccessKey = _configuration["AWS:AccessKey"] ?? "";
                _config.AWS.SecretKey = _configuration["AWS:SecretKey"] ?? "";
                _config.AWS.Region = _configuration["AWS:Region"] ?? "";
                _config.AWS.BucketName = _configuration["AWS:BucketName"] ?? "";
            }
        }
        return _config;
    }
}

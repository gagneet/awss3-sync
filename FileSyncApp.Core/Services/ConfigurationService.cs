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
                    Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
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
                _config.AWS.AccessKey = _configuration["AWS:AccessKey"] ?? _config.AWS.AccessKey;
                _config.AWS.SecretKey = _configuration["AWS:SecretKey"] ?? _config.AWS.SecretKey;
                _config.AWS.Region = _configuration["AWS:Region"] ?? _config.AWS.Region;
                _config.AWS.BucketName = _configuration["AWS:BucketName"] ?? _config.AWS.BucketName;
            }
        }
        return _config;
    }
}

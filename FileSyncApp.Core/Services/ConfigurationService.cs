using FileSyncApp.Core.Interfaces;
using FileSyncApp.Core.Models;
using Microsoft.Extensions.Configuration;

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
            _configuration.Bind(_config);

            // Legacy support if not bound correctly
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json;
using AWSS3Sync.Models;

namespace AWSS3Sync.Services
{
    public class ConfigurationService
    {
        private static AppConfig? _config;

        public static AppConfig GetConfiguration()
        {
            if (_config == null)
            {
                LoadConfiguration();
            }
            return _config ?? throw new InvalidOperationException("Configuration could not be loaded");
        }

        private static void LoadConfiguration()
        {
            try
            {
                string configPath = FindConfigurationFile();
                if (string.IsNullOrEmpty(configPath))
                {
                    var message = "appsettings.json not found. Please ensure the file exists in one of these locations:\n\n" +
                                 $"1. Application directory: {Application.StartupPath}\n" +
                                 $"2. Current directory: {Directory.GetCurrentDirectory()}\n" +
                                 $"3. Project root (set 'Copy to Output Directory' to 'Copy always')\n\n" +
                                 "The file should contain your AWS configuration:\n" +
                                 "{\n" +
                                 "  \"AWS\": {\n" +
                                 "    \"AccessKey\": \"your-access-key\",\n" +
                                 "    \"SecretKey\": \"your-secret-key\",\n" +
                                 "    \"Region\": \"your-region\",\n" +
                                 "    \"BucketName\": \"your-bucket-name\"\n" +
                                 "  }\n" +
                                 "}";

                    MessageBox.Show(message, "Configuration File Not Found",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    throw new FileNotFoundException("appsettings.json not found in any expected location.");
                }

                string json = File.ReadAllText(configPath);
                _config = JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();

                // Validate configuration
                ValidateConfiguration(_config);
            }
            catch (Exception ex)
            {
                if (!(ex is FileNotFoundException))
                {
                    MessageBox.Show($"Error loading configuration: {ex.Message}", "Configuration Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                throw;
            }
        }

        private static string FindConfigurationFile()
        {
            // List of possible locations to look for the config file
            var possiblePaths = new[]
            {
                Path.Combine(Application.StartupPath, "appsettings.json"),           // Output directory
                Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"),   // Current directory
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json"), // Base directory
                "appsettings.json" // Current working directory
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return string.Empty;
        }

        private static void ValidateConfiguration(AppConfig config)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(config.AWS.AccessKey))
                errors.Add("AWS AccessKey is missing or empty");

            if (string.IsNullOrWhiteSpace(config.AWS.SecretKey))
                errors.Add("AWS SecretKey is missing or empty");

            if (string.IsNullOrWhiteSpace(config.AWS.Region))
                errors.Add("AWS Region is missing or empty");

            if (string.IsNullOrWhiteSpace(config.AWS.BucketName))
                errors.Add("AWS BucketName is missing or empty");

            if (errors.Count > 0)
            {
                var message = "Configuration validation failed:\n\n" + string.Join("\n", errors);
                throw new InvalidOperationException(message);
            }
        }
    }
}
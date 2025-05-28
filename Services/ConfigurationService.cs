using System;
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json;
using S3FileManager.Models;

namespace S3FileManager.Services
{
    public class ConfigurationService
    {
        private static AppConfig _config;

        public static AppConfig GetConfiguration()
        {
            if (_config == null)
            {
                LoadConfiguration();
            }
            return _config;
        }

        private static void LoadConfiguration()
        {
            try
            {
                string configPath = Path.Combine(Application.StartupPath, "appsettings.json");
                if (!File.Exists(configPath))
                {
                    throw new FileNotFoundException("appsettings.json not found. Please create the configuration file.");
                }

                string json = File.ReadAllText(configPath);
                _config = JsonConvert.DeserializeObject<AppConfig>(json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading configuration: {ex.Message}", "Configuration Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw;
            }
        }
    }
}
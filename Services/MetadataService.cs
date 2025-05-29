using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using S3FileManager.Models;

namespace S3FileManager.Services
{
    public class MetadataService
    {
        private readonly string _metadataPath;
        private Dictionary<string, List<UserRole>> _fileAccessRoles = new Dictionary<string, List<UserRole>>();

        public MetadataService()
        {
            _metadataPath = Path.Combine(Application.StartupPath, "file_permissions.json");
            LoadMetadata();
        }

        private void LoadMetadata()
        {
            try
            {
                if (File.Exists(_metadataPath))
                {
                    string json = File.ReadAllText(_metadataPath);
                    var data = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json);
                    if (data != null)
                    {
                        _fileAccessRoles = data.ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value.Select(r => Enum.Parse<UserRole>(r)).ToList()
                        );
                    }
                }
                else
                {
                    _fileAccessRoles = new Dictionary<string, List<UserRole>>();
                }
            }
            catch
            {
                _fileAccessRoles = new Dictionary<string, List<UserRole>>();
            }
        }

        private void SaveMetadata()
        {
            try
            {
                var data = _fileAccessRoles.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Select(r => r.ToString()).ToList()
                );
                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(_metadataPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving metadata: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public async Task<List<UserRole>> GetFileAccessRolesAsync(string key)
        {
            await Task.CompletedTask; // Make it async for future database integration

            if (_fileAccessRoles.ContainsKey(key))
            {
                return _fileAccessRoles[key];
            }

            // Check if parent folder has permissions
            var parentFolder = GetParentFolder(key);
            while (!string.IsNullOrEmpty(parentFolder))
            {
                if (_fileAccessRoles.ContainsKey(parentFolder + "/"))
                {
                    return _fileAccessRoles[parentFolder + "/"];
                }
                parentFolder = GetParentFolder(parentFolder);
            }

            // Default: only administrators can access
            return new List<UserRole> { UserRole.Administrator };
        }

        public async Task SetFileAccessRolesAsync(string key, List<UserRole> roles)
        {
            await Task.CompletedTask;
            _fileAccessRoles[key] = new List<UserRole>(roles);
            SaveMetadata();
        }

        public async Task RemoveFileAccessRolesAsync(string key)
        {
            await Task.CompletedTask;
            _fileAccessRoles.Remove(key);
            SaveMetadata();
        }

        private string GetParentFolder(string key)
        {
            var lastSlash = key.LastIndexOf('/');
            return lastSlash > 0 ? key.Substring(0, lastSlash) : "";
        }
    }
}
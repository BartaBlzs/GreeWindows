using System;
using System.IO;
using GreeAC.Library.Models;
using Newtonsoft.Json;

namespace GreeAC.Library.Managers
{
    public class ConfigManager
    {
        private readonly string _configPath;

        public ConfigManager(string configPath = "ac_config.json")
        {
            _configPath = configPath;
        }

        public AppConfig LoadConfig()
        {
            if (!File.Exists(_configPath))
            {
                return new AppConfig();
            }

            try
            {
                var json = File.ReadAllText(_configPath);
                return JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
            }
            catch (Exception)
            {
                return new AppConfig();
            }
        }

        public void SaveConfig(AppConfig config)
        {
            var json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(_configPath, json);
        }

        public void UpdateFavoriteDevice(string deviceId)
        {
            var config = LoadConfig();
            config.FavoriteDeviceId = deviceId;
            SaveConfig(config);
        }

        public void UpdateDeviceKey(string deviceId, string newKey)
        {
            var config = LoadConfig();
            var device = config.Devices.Find(d => d.Id == deviceId);

            if (device != null)
            {
                device.Key = newKey;
                device.LastUpdated = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                SaveConfig(config);
            }
        }

        public void UpdateDevices(AppConfig config)
        {
            SaveConfig(config);
        }
    }
}
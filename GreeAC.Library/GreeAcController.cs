using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GreeAC.Library.Helpers;
using GreeAC.Library.Managers;
using GreeAC.Library.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GreeAC.Library
{
    public class GreeAcController
    {
        private readonly ConfigManager _configManager;
        private AppConfig _config;
        private GreeDevice _currentDevice;

        public event EventHandler<string> LogMessage;
        public event EventHandler<AcStatus> StatusUpdated;

        public GreeDevice CurrentDevice => _currentDevice;
        public bool IsConnected => _currentDevice != null && !string.IsNullOrEmpty(_currentDevice.Key);
        public List<GreeDevice> AvailableDevices => _config?.Devices ?? new List<GreeDevice>();

        public GreeAcController(string configPath = "ac_config.json")
        {
            _configManager = new ConfigManager(configPath);
            _config = _configManager.LoadConfig();
        }

        private void Log(string message)
        {
            LogMessage?.Invoke(this, message);
        }

        public async Task<List<GreeDevice>> SearchDevicesAsync()
        {
            Log("Searching for AC devices...");
            Log($"Using broadcast address: {_config.Broadcast}");

            var devices = new List<GreeDevice>();

            try
            {
                using (var udpClient = new UdpClient())
                {
                    udpClient.EnableBroadcast = true;
                    udpClient.Client.ReceiveTimeout = 5000;

                    var scanMessage = Encoding.UTF8.GetBytes("{\"t\":\"scan\"}");
                    var broadcastEp = new IPEndPoint(IPAddress.Parse(_config.Broadcast), 7000);

                    Log($"Sending scan request to {_config.Broadcast}:7000");
                    await udpClient.SendAsync(scanMessage, scanMessage.Length, broadcastEp);

                    var startTime = DateTime.Now;
                    var timeout = TimeSpan.FromSeconds(5);

                    while ((DateTime.Now - startTime) < timeout)
                    {
                        try
                        {
                            if (udpClient.Available > 0)
                            {
                                var result = await udpClient.ReceiveAsync();
                                var data = Encoding.UTF8.GetString(result.Buffer);

                                Log($"Received response from {result.RemoteEndPoint.Address}");

                                var lastBrace = data.LastIndexOf('}');
                                if (lastBrace >= 0)
                                {
                                    data = data.Substring(0, lastBrace + 1);
                                }

                                var response = JsonConvert.DeserializeObject<JObject>(data);

                                // Initial detection
                                string encryptionType = response.ContainsKey("tag") ? "GCM" : "ECB";
                                Log($"Initial encryption type: {encryptionType}");

                                string decryptedPack;

                                // Try to decrypt based on initial detection
                                if (encryptionType == "GCM")
                                {
                                    decryptedPack = EncryptionHelper.DecryptGcmGeneric(
                                        response["pack"].ToString(),
                                        response["tag"].ToString()
                                    );
                                }
                                else
                                {
                                    // Decrypt using ECB for scan response
                                    decryptedPack = EncryptionHelper.DecryptEcb(
                                        response["pack"].ToString(),
                                        EncryptionHelper.GenericKey
                                    );
                                }

                                Log($"Decrypted pack: {decryptedPack}");

                                var pack = JsonConvert.DeserializeObject<JObject>(decryptedPack);

                                // Check version to determine actual encryption type
                                if (pack.ContainsKey("ver"))
                                {
                                    var ver = pack["ver"].ToString();
                                    Log($"Device version: {ver}");

                                    var match = Regex.Match(ver, @"V(\d+)");
                                    if (match.Success && int.Parse(match.Groups[1].Value) >= 2)
                                    {
                                        encryptionType = "GCM";
                                        Log("Switching to GCM encryption based on version");
                                    }
                                }

                                var device = new GreeDevice
                                {
                                    Ip = result.RemoteEndPoint.Address.ToString(),
                                    Port = 7000,
                                    Id = pack["cid"]?.ToString() ?? response["cid"]?.ToString() ?? "unknown",
                                    Name = pack["name"]?.ToString() ?? "Unknown",
                                    Encryption = encryptionType
                                };

                                Log($"Found device: {device.Ip} - {device.Name} (ID: {device.Id}, Encryption: {device.Encryption})");

                                // Bind immediately to get key
                                if (await BindDeviceAsync(device))
                                {
                                    devices.Add(device);
                                }
                            }
                            else
                            {
                                await Task.Delay(100);
                            }
                        }
                        catch (SocketException ex)
                        {
                            Log($"Socket timeout or error: {ex.Message}");
                            break;
                        }
                        catch (Exception ex)
                        {
                            Log($"Error processing device: {ex.Message}");
                            Log($"Stack trace: {ex.StackTrace}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Search error: {ex.Message}");
                Log($"Stack trace: {ex.StackTrace}");
            }

            _config.Devices = devices;
            _configManager.SaveConfig(_config);

            Log($"Search completed. Found {devices.Count} device(s)");
            return devices;
        }

        private async Task<bool> BindDeviceAsync(GreeDevice device)
        {
            try
            {
                Log($"Binding to device: {device.Id} at {device.Ip}");

                var pack = $"{{\"mac\":\"{device.Id}\",\"t\":\"bind\",\"uid\":0}}";
                var encrypted = EncryptionHelper.EncryptGcmGeneric(pack);

                var request = new JObject
                {
                    ["cid"] = "app",
                    ["i"] = 1,
                    ["t"] = "pack",
                    ["uid"] = 0,
                    ["tcid"] = device.Id,
                    ["pack"] = encrypted.Pack,
                    ["tag"] = encrypted.Tag
                };

                Log($"Bind request: {request.ToString(Formatting.None)}");

                var response = await SendCommandAsync(device.Ip, request.ToString(Formatting.None));

                if (response != null && response.ContainsKey("pack"))
                {
                    var decryptedPack = EncryptionHelper.DecryptGcmGeneric(
                        response["pack"].ToString(),
                        response["tag"].ToString()
                    );

                    Log($"Bind response: {decryptedPack}");

                    var bindResponse = JsonConvert.DeserializeObject<JObject>(decryptedPack);

                    if (bindResponse["t"]?.ToString().ToLower() == "bindok")
                    {
                        device.Key = bindResponse["key"]?.ToString();
                        device.LastUpdated = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        Log($"Bound successfully. Key: {device.Key}");
                        return true;
                    }
                    else
                    {
                        Log($"Bind failed: {bindResponse["t"]?.ToString()}");
                    }
                }
                else
                {
                    Log("No response from device");
                }
            }
            catch (Exception ex)
            {
                Log($"Bind failed: {ex.Message}");
                Log($"Stack trace: {ex.StackTrace}");
            }

            return false;
        }

        private async Task<JObject> SendCommandAsync(string ip, string data)
        {
            try
            {
                using (var udpClient = new UdpClient())
                {
                    udpClient.Client.ReceiveTimeout = 5000;

                    var bytes = Encoding.UTF8.GetBytes(data);
                    var endPoint = new IPEndPoint(IPAddress.Parse(ip), 7000);

                    Log($"Sending to {ip}:7000 - {data}");
                    await udpClient.SendAsync(bytes, bytes.Length, endPoint);

                    var result = await udpClient.ReceiveAsync();
                    var response = Encoding.UTF8.GetString(result.Buffer);

                    Log($"Received from {ip}: {response}");

                    var lastBrace = response.LastIndexOf('}');
                    if (lastBrace >= 0)
                    {
                        response = response.Substring(0, lastBrace + 1);
                    }

                    return JsonConvert.DeserializeObject<JObject>(response);
                }
            }
            catch (Exception ex)
            {
                Log($"SendCommand error: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> SetFavoriteDeviceAsync(string deviceId)
        {
            var device = _config.Devices.FirstOrDefault(d => d.Id == deviceId);

            if (device == null)
            {
                Log($"Device {deviceId} not found");
                return false;
            }

            _config.FavoriteDeviceId = deviceId;
            _configManager.SaveConfig(_config);
            _currentDevice = device;

            Log($"Favorite device set to: {device.DisplayName}");
            return true;
        }

        public async Task<bool> ConnectToFavoriteAsync()
        {
            if (string.IsNullOrEmpty(_config.FavoriteDeviceId))
            {
                Log("No favorite device set");
                return false;
            }

            var device = _config.FavoriteDevice;

            if (device == null)
            {
                Log("Favorite device not found in config");
                return false;
            }

            _currentDevice = device;

            // Try to verify connection
            try
            {
                var status = await GetStatusAsync();
                if (status != null)
                {
                    Log($"Connected to favorite device: {device.DisplayName}");
                    return true;
                }
            }
            catch
            {
                // Connection failed, try rebind
                Log("Rebinding to favorite device...");
                if (await BindDeviceAsync(device))
                {
                    _configManager.UpdateDeviceKey(device.Id, device.Key);
                    Log($"Reconnected to: {device.DisplayName}");
                    return true;
                }
            }

            return false;
        }

        public async Task<AcStatus> GetStatusAsync()
        {
            if (_currentDevice == null)
            {
                throw new InvalidOperationException("No device selected");
            }

            var pack = $"{{\"cols\":[\"Pow\",\"Mod\",\"SetTem\",\"WdSpd\",\"Air\",\"Blo\",\"Health\",\"SwhSlp\",\"Lig\",\"SwingLfRig\",\"SwUpDn\",\"Quiet\",\"Tur\"],\"mac\":\"{_currentDevice.Id}\",\"t\":\"status\"}}";
            var encrypted = EncryptionHelper.EncryptGcm(pack, _currentDevice.Key);

            var request = new JObject
            {
                ["cid"] = "app",
                ["i"] = 0,
                ["t"] = "pack",
                ["uid"] = 0,
                ["tcid"] = _currentDevice.Id,
                ["pack"] = encrypted.Pack,
                ["tag"] = encrypted.Tag
            };

            var response = await SendCommandAsync(_currentDevice.Ip, request.ToString());

            if (response != null && response.ContainsKey("pack"))
            {
                var decryptedPack = EncryptionHelper.DecryptGcm(
                    response["pack"].ToString(),
                    response["tag"].ToString(),
                    _currentDevice.Key
                );

                var statusData = JsonConvert.DeserializeObject<JObject>(decryptedPack);
                var cols = statusData["cols"].ToObject<List<string>>();
                var dat = statusData["dat"].ToObject<List<int>>();

                var status = new AcStatus();

                for (int i = 0; i < cols.Count && i < dat.Count; i++)
                {
                    switch (cols[i])
                    {
                        case "Pow": status.Power = dat[i] == 1; break;
                        case "Mod": status.Mode = dat[i]; break;
                        case "SetTem": status.Temperature = dat[i]; break;
                        case "WdSpd": status.FanSpeed = dat[i]; break;
                        case "Tur": status.Turbo = dat[i] == 1; break;
                        case "Quiet": status.Quiet = dat[i] == 1; break;
                        case "Lig": status.Light = dat[i] == 1; break;
                        case "Health": status.Health = dat[i] == 1; break;
                        case "SwUpDn": status.SwingVertical = dat[i]; break;
                        case "SwingLfRig": status.SwingHorizontal = dat[i]; break;
                    }
                }

                StatusUpdated?.Invoke(this, status);
                return status;
            }

            return null;
        }

        public async Task<bool> SetParametersAsync(Dictionary<string, int> parameters)
        {
            if (_currentDevice == null)
            {
                throw new InvalidOperationException("No device selected");
            }

            var opts = string.Join(",", parameters.Keys.Select(k => $"\"{k}\""));
            var ps = string.Join(",", parameters.Values);

            var pack = $"{{\"opt\":[{opts}],\"p\":[{ps}],\"t\":\"cmd\"}}";
            var encrypted = EncryptionHelper.EncryptGcm(pack, _currentDevice.Key);

            var request = new JObject
            {
                ["cid"] = "app",
                ["i"] = 0,
                ["t"] = "pack",
                ["uid"] = 0,
                ["tcid"] = _currentDevice.Id,
                ["pack"] = encrypted.Pack,
                ["tag"] = encrypted.Tag
            };

            var response = await SendCommandAsync(_currentDevice.Ip, request.ToString());

            if (response != null && response.ContainsKey("pack"))
            {
                var decryptedPack = EncryptionHelper.DecryptGcm(
                    response["pack"].ToString(),
                    response["tag"].ToString(),
                    _currentDevice.Key
                );

                var result = JsonConvert.DeserializeObject<JObject>(decryptedPack);

                if (result["r"]?.ToObject<int>() == 200)
                {
                    Log("Parameters set successfully");
                    return true;
                }
            }

            return false;
        }

        // Convenience methods
        public Task<bool> SetPowerAsync(bool on) =>
            SetParametersAsync(new Dictionary<string, int> { ["Pow"] = on ? 1 : 0 });

        public Task<bool> SetTemperatureAsync(int temperature) =>
            SetParametersAsync(new Dictionary<string, int> { ["SetTem"] = temperature });

        public Task<bool> SetModeAsync(int mode) =>
            SetParametersAsync(new Dictionary<string, int> { ["Mod"] = mode });

        public Task<bool> SetFanSpeedAsync(int speed) =>
            SetParametersAsync(new Dictionary<string, int> { ["WdSpd"] = speed });
    }
}
using System.Collections.Generic;
using Newtonsoft.Json;

namespace GreeAC.Library.Models
{
    public class AppConfig
    {
        [JsonProperty("broadcast")]
        public string Broadcast { get; set; } = "192.168.0.255";

        [JsonProperty("devices")]
        public List<GreeDevice> Devices { get; set; } = new List<GreeDevice>();

        [JsonProperty("favorite_device_id")]
        public string FavoriteDeviceId { get; set; }

        [JsonIgnore]
        public GreeDevice FavoriteDevice =>
            Devices?.Find(d => d.Id == FavoriteDeviceId);
    }
}
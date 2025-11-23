using System;
using Newtonsoft.Json;

namespace GreeAC.Library.Models;

public class GreeDevice
{
    [JsonProperty("ip")]
    public string Ip { get; set; }

    [JsonProperty("port")]
    public int Port { get; set; } = 7000;

    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("key")]
    public string Key { get; set; }

    [JsonProperty("encryption")]
    public string Encryption { get; set; } = "GCM";

    [JsonProperty("last_updated")]
    public long LastUpdated { get; set; }

    public DateTime LastUpdatedDateTime =>
        DateTimeOffset.FromUnixTimeSeconds(LastUpdated).LocalDateTime;

    public string DisplayName => string.IsNullOrEmpty(Name) || Name == "Unknown"
        ? $"{Ip} ({Id})"
        : $"{Name} ({Ip})";

    public override string ToString()
    {
        return DisplayName;
    }
}
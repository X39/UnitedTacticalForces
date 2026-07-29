using System.Text.Json.Serialization;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

internal sealed class HetznerVolumeDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("linux_device")]
    public string? LinuxDevice { get; set; }

    [JsonPropertyName("server")]
    public long? Server { get; set; }

    [JsonPropertyName("location")]
    public HetznerLocationDto? Location { get; set; }

    [JsonPropertyName("protection")]
    public HetznerVolumeProtectionDto? Protection { get; set; }

    [JsonPropertyName("labels")]
    public Dictionary<string, string>? Labels { get; set; }
}

internal sealed class HetznerVolumeProtectionDto
{
    [JsonPropertyName("delete")]
    public bool Delete { get; set; }
}

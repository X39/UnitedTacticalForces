using System.Text.Json.Serialization;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

internal sealed class HetznerVolumeResponse
{
    [JsonPropertyName("volume")]
    public HetznerVolumeDto? Volume { get; set; }
}

using System.Text.Json.Serialization;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

internal sealed class HetznerVolumesResponse
{
    [JsonPropertyName("volumes")]
    public required List<HetznerVolumeDto>? Volumes { get; set; }

    [JsonPropertyName("meta")]
    public required HetznerMetaDto? Meta { get; set; }
}

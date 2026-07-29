using System.Text.Json.Serialization;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

internal sealed class HetznerPrimaryIpsResponse
{
    [JsonPropertyName("primary_ips")]
    public required List<HetznerPrimaryIpDto>? PrimaryIps { get; set; }

    [JsonPropertyName("meta")]
    public required HetznerMetaDto? Meta { get; set; }
}

using System.Text.Json.Serialization;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

internal sealed class HetznerFirewallsResponse
{
    [JsonPropertyName("firewalls")]
    public required List<HetznerFirewallDto>? Firewalls { get; set; }

    [JsonPropertyName("meta")]
    public required HetznerMetaDto? Meta { get; set; }
}

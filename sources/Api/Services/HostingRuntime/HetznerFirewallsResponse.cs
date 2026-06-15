using System.Text.Json.Serialization;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

internal sealed class HetznerFirewallsResponse
{
    [JsonPropertyName("firewalls")]
    public List<HetznerFirewallDto>? Firewalls { get; set; }

    [JsonPropertyName("meta")]
    public HetznerMetaDto? Meta { get; set; }
}
using System.Text.Json.Serialization;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

internal sealed class HetznerFirewallResponse
{
    [JsonPropertyName("firewall")]
    public HetznerFirewallDto? Firewall { get; set; }
}
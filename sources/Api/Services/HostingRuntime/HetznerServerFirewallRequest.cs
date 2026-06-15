using System.Text.Json.Serialization;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

internal sealed record HetznerServerFirewallRequest(
    [property: JsonPropertyName("firewall")] long Firewall);
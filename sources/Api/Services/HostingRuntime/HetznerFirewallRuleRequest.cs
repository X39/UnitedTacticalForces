using System.Text.Json.Serialization;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

internal sealed record HetznerFirewallRuleRequest(
    [property: JsonPropertyName("direction")] string Direction,
    [property: JsonPropertyName("protocol")] string Protocol,
    [property: JsonPropertyName("port")] string Port,
    [property: JsonPropertyName("source_ips")] IReadOnlyList<string> SourceIps,
    [property: JsonPropertyName("description")] string Description);
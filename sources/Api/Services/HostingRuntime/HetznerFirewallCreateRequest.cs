using System.Text.Json.Serialization;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

internal sealed record HetznerFirewallCreateRequest(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("rules")] IReadOnlyList<HetznerFirewallRuleRequest> Rules,
    [property: JsonPropertyName("labels")] IReadOnlyDictionary<string, string> Labels);
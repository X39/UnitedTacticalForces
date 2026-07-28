using System.Text.Json;
using System.Text.Json.Serialization;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

internal sealed class HetznerFirewallDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("labels")]
    public Dictionary<string, string>? Labels { get; set; }

    [JsonPropertyName("applied_to")]
    public List<JsonElement>? AppliedTo { get; set; }

    [JsonPropertyName("rules")]
    public List<HetznerFirewallRuleDto>? Rules { get; set; }
}

internal sealed class HetznerFirewallRuleDto
{
    [JsonPropertyName("direction")]
    public string? Direction { get; set; }

    [JsonPropertyName("protocol")]
    public string? Protocol { get; set; }

    [JsonPropertyName("port")]
    public string? Port { get; set; }

    [JsonPropertyName("source_ips")]
    public List<string>? SourceIps { get; set; }

    [JsonPropertyName("destination_ips")]
    public List<string>? DestinationIps { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

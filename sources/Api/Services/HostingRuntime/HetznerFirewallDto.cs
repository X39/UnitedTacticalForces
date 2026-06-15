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
}
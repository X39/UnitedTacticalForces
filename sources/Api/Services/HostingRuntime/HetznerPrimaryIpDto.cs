using System.Text.Json.Serialization;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

internal sealed class HetznerPrimaryIpDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("ip")]
    public string? Ip { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("assignee_id")]
    public long? AssigneeId { get; set; }

    [JsonPropertyName("assignee_type")]
    public string? AssigneeType { get; set; }

    [JsonPropertyName("auto_delete")]
    public bool AutoDelete { get; set; }

    [JsonPropertyName("labels")]
    public Dictionary<string, string>? Labels { get; set; }
}
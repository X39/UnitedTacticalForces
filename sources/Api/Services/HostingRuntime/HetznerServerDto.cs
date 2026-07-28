using System.Text.Json.Serialization;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

internal sealed class HetznerServerDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("labels")]
    public Dictionary<string, string>? Labels { get; set; }

    [JsonPropertyName("public_net")]
    public HetznerPublicNetDto? PublicNet { get; set; }

    [JsonPropertyName("server_type")]
    public HetznerResourceReferenceDto? ServerType { get; set; }

    [JsonPropertyName("image")]
    public HetznerResourceReferenceDto? Image { get; set; }

    [JsonPropertyName("location")]
    public HetznerResourceReferenceDto? Location { get; set; }

    [JsonPropertyName("datacenter")]
    public HetznerDatacenterDto? Datacenter { get; set; }
}

internal sealed class HetznerResourceReferenceDto
{
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

internal sealed class HetznerDatacenterDto
{
    [JsonPropertyName("location")]
    public HetznerResourceReferenceDto? Location { get; set; }
}

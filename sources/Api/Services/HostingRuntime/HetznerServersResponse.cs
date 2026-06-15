using System.Text.Json.Serialization;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

internal sealed class HetznerServersResponse
{
    [JsonPropertyName("servers")]
    public List<HetznerServerDto>? Servers { get; set; }

    [JsonPropertyName("meta")]
    public HetznerMetaDto? Meta { get; set; }
}
using System.Text.Json.Serialization;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

internal sealed class HetznerServersResponse
{
    [JsonPropertyName("servers")]
    public required List<HetznerServerDto>? Servers { get; set; }

    [JsonPropertyName("meta")]
    public required HetznerMetaDto? Meta { get; set; }
}

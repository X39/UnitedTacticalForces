using System.Text.Json.Serialization;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

internal sealed class HetznerServerTypesResponse
{
    [JsonPropertyName("server_types")]
    public required IReadOnlyList<HetznerServerTypeDto>? ServerTypes { get; set; }

    [JsonPropertyName("meta")]
    public required HetznerMetaDto? Meta { get; set; }
}

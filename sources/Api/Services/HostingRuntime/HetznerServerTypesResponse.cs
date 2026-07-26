using System.Text.Json.Serialization;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

internal sealed class HetznerServerTypesResponse
{
    [JsonPropertyName("server_types")]
    public IReadOnlyList<HetznerServerTypeDto>? ServerTypes { get; set; }

    public HetznerMetaDto? Meta { get; set; }
}

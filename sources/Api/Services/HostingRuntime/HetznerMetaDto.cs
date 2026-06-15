using System.Text.Json.Serialization;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

internal sealed class HetznerMetaDto
{
    [JsonPropertyName("pagination")]
    public HetznerPaginationDto? Pagination { get; set; }
}
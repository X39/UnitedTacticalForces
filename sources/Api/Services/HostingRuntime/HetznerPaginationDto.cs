using System.Text.Json.Serialization;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

internal sealed class HetznerPaginationDto
{
    [JsonPropertyName("next_page")]
    public int? NextPage { get; set; }
}
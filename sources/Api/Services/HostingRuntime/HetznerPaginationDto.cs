using System.Text.Json.Serialization;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

internal sealed class HetznerPaginationDto
{
    [JsonPropertyName("next_page")]
    public required long? NextPage { get; set; }
}

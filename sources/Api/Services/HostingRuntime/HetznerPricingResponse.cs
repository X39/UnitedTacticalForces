using System.Text.Json.Serialization;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

internal sealed class HetznerPricingResponse
{
    [JsonPropertyName("pricing")]
    public required HetznerPricingDto? Pricing { get; set; }
}

internal sealed class HetznerPricingDto
{
    [JsonPropertyName("currency")]
    public required string? Currency { get; set; }
}

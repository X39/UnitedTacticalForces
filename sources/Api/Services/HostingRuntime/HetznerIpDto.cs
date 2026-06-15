using System.Text.Json.Serialization;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

internal sealed class HetznerIpDto
{
    [JsonPropertyName("ip")]
    public string? Ip { get; set; }
}
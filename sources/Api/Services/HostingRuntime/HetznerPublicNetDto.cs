using System.Text.Json.Serialization;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

internal sealed class HetznerPublicNetDto
{
    [JsonPropertyName("ipv4")]
    public HetznerIpDto? Ipv4 { get; set; }

    [JsonPropertyName("ipv6")]
    public HetznerIpDto? Ipv6 { get; set; }
}
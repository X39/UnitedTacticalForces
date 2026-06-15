using System.Text.Json.Serialization;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

internal sealed record HetznerServerPublicNetRequest(
    [property: JsonPropertyName("enable_ipv4")] bool EnableIpv4,
    [property: JsonPropertyName("enable_ipv6")] bool EnableIpv6,
    [property: JsonPropertyName("ipv4")] long Ipv4);
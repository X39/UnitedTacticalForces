using System.Text.Json.Serialization;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

internal sealed class HetznerPrimaryIpResponse
{
    [JsonPropertyName("primary_ip")]
    public HetznerPrimaryIpDto? PrimaryIp { get; set; }
}
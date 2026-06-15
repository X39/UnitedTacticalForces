using System.Text.Json.Serialization;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

internal sealed class HetznerServerResponse
{
    [JsonPropertyName("server")]
    public HetznerServerDto? Server { get; set; }
}
using System.Text.Json.Serialization;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

internal sealed record HetznerVolumeServerRequest(
    [property: JsonPropertyName("server")] long ServerId);

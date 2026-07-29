using System.Text.Json.Serialization;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

internal sealed record HetznerVolumeCreateRequest(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("size")] long SizeGiB,
    [property: JsonPropertyName("location")] string Location,
    [property: JsonPropertyName("labels")] IReadOnlyDictionary<string, string> Labels,
    [property: JsonPropertyName("automount")] bool Automount = false);

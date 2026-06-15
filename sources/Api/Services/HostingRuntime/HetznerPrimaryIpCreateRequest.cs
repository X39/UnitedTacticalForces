using System.Text.Json.Serialization;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

internal sealed record HetznerPrimaryIpCreateRequest(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("location")] string Location,
    [property: JsonPropertyName("auto_delete")] bool AutoDelete,
    [property: JsonPropertyName("labels")] IReadOnlyDictionary<string, string> Labels);
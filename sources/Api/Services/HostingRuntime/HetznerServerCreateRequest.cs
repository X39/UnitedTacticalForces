using System.Text.Json.Serialization;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

internal sealed record HetznerServerCreateRequest(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("server_type")] string ServerType,
    [property: JsonPropertyName("image")] string Image,
    [property: JsonPropertyName("location")] string Location,
    [property: JsonPropertyName("start_after_create")] bool StartAfterCreate,
    [property: JsonPropertyName("user_data")] string UserData,
    [property: JsonPropertyName("public_net")] HetznerServerPublicNetRequest PublicNet,
    [property: JsonPropertyName("firewalls")] IReadOnlyList<HetznerServerFirewallRequest> Firewalls,
    [property: JsonPropertyName("labels")] IReadOnlyDictionary<string, string> Labels);
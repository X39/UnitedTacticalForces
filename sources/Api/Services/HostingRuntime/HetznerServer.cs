namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

internal sealed record HetznerServer(
    long Id,
    string Name,
    string Status,
    string? PublicIpv4,
    string? PublicIpv6,
    HetznerResourceReference ServerType,
    HetznerResourceReference Image,
    HetznerResourceReference Location,
    IReadOnlyDictionary<string, string> Labels);

internal sealed record HetznerResourceReference(long? Id, string Name);

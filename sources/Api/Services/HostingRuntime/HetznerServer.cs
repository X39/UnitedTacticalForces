namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

internal sealed record HetznerServer(
    long Id,
    string Name,
    string Status,
    string? PublicIpv4,
    string? PublicIpv6,
    IReadOnlyDictionary<string, string> Labels);
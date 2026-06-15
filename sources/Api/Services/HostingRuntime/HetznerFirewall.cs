namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

internal sealed record HetznerFirewall(
    long Id,
    string Name,
    int AppliedToCount,
    IReadOnlyList<long> AppliedServerIds,
    IReadOnlyDictionary<string, string> Labels);
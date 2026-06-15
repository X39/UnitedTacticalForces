namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

internal sealed record HetznerPrimaryIp(
    long Id,
    string Name,
    string Ip,
    string Type,
    long? AssigneeId,
    string? AssigneeType,
    bool AutoDelete,
    IReadOnlyDictionary<string, string> Labels);
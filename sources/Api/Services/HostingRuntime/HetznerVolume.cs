namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

internal sealed record HetznerVolume(
    long Id,
    string Name,
    long SizeGiB,
    string Status,
    string LinuxDevice,
    long? ServerId,
    bool DeleteProtected,
    HetznerResourceReference Location,
    IReadOnlyDictionary<string, string> Labels);

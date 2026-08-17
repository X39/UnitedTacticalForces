namespace X39.UnitedTacticalForces.CloudWorker;

public sealed record RestrictedProcessOptions(
    int? GameUserId,
    int? GameGroupId,
    string? PrivilegeDropPath,
    int? SteamUserId);

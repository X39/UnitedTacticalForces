namespace X39.UnitedTacticalForces.CloudWorker;

public sealed record SteamCmdWorkerOptions(
    string SteamCmdPath,
    string TemporaryDirectory,
    string? SteamHomeDirectory,
    int? GameUserId,
    int? GameGroupId,
    string? PrivilegeDropPath,
    int? SteamUserId);

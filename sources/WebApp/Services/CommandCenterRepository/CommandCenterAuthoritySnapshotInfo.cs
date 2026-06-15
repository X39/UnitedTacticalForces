namespace X39.UnitedTacticalForces.WebApp.Services.CommandCenterRepository;

public record CommandCenterAuthoritySnapshotInfo
{
    public long MemberCount { get; init; }

    public long RoleCount { get; init; }

    public long ClaimCount { get; init; }

    public CommandCenterAuthorityRoleInfo[] Roles { get; init; } = [];

    public CommandCenterAuthorityClaimInfo[] Claims { get; init; } = [];
}

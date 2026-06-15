namespace X39.UnitedTacticalForces.WebApp.Services.CommandCenterRepository;

public record CommandCenterCommunitySnapshotInfo
{
    public long MemberCount { get; init; }

    public long VerifiedMemberCount { get; init; }

    public long UnverifiedMemberCount { get; init; }

    public long BannedMemberCount { get; init; }

    public long DeletedMemberCount { get; init; }

    public long ActiveVereinMemberCount { get; init; }

    public long SuspendedVereinMemberCount { get; init; }

    public long EndedVereinMemberCount { get; init; }

    public long RoleCount { get; init; }

    public long ClaimCount { get; init; }

    public CommandCenterCommunityMemberInfo[] Members { get; init; } = [];
}

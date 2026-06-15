namespace X39.UnitedTacticalForces.WebApp.Services.CommandCenterRepository;

public record CommandCenterCommunityMemberInfo
{
    public Guid PrimaryKey { get; init; }

    public string Nickname { get; init; } = string.Empty;

    public byte[] Avatar { get; init; } = [];

    public string AvatarMimeType { get; init; } = string.Empty;

    public bool IsVerified { get; init; }

    public bool IsBanned { get; init; }

    public bool IsDeleted { get; init; }

    public string? VereinMembershipStatus { get; init; }

    public string? VereinMemberNumber { get; init; }

    public bool IsActiveVereinMember { get; init; }

    public DateTimeOffset? VereinMembershipStarted { get; init; }

    public DateTimeOffset? VereinMembershipEnded { get; init; }

    public string[] RoleTitles { get; init; } = [];

    public int DirectClaimCount { get; init; }
}

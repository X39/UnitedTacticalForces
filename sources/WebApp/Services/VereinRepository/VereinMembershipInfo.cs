using X39.UnitedTacticalForces.Common.Verein;

namespace X39.UnitedTacticalForces.WebApp.Services.VereinRepository;

public record VereinMembershipInfo
{
    public long PrimaryKey { get; init; }

    public Guid UserFk { get; init; }

    public string? UserNickname { get; init; }

    public string? MemberNumber { get; init; }

    public EVereinMembershipStatus Status { get; init; }

    public bool IsActiveMember { get; init; }

    public DateTimeOffset TimeStampStarted { get; init; }

    public DateTimeOffset? TimeStampEnded { get; init; }

    public string Notes { get; init; } = string.Empty;

    public DateTimeOffset TimeStampCreated { get; init; }

    public DateTimeOffset TimeStampUpdated { get; init; }
}

using X39.UnitedTacticalForces.Common.Verein;

namespace X39.UnitedTacticalForces.WebApp.Services.VereinRepository;

public record VereinMembershipUpdateInfo
{
    public string? MemberNumber { get; init; }

    public EVereinMembershipStatus Status { get; init; } = EVereinMembershipStatus.Active;

    public DateTimeOffset? TimeStampStarted { get; init; }

    public DateTimeOffset? TimeStampEnded { get; init; }

    public string? Notes { get; init; }

    public string? Reason { get; init; }
}

using X39.UnitedTacticalForces.Common.Verein;

namespace X39.UnitedTacticalForces.WebApp.Services.VereinRepository;

public record VereinMembershipRequestTransitionInfo
{
    public EVereinMembershipRequestStatus Status { get; init; }

    public string? Reason { get; init; }

    public string? MemberNumber { get; init; }

    public string? Notes { get; init; }
}

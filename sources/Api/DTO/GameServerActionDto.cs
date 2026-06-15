using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Server-authoritative action presentation and availability metadata.
/// </summary>
public sealed record GameServerActionDto
{
    public string Id { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;
    public string Group { get; init; } = string.Empty;
    public int Order { get; init; }
    public bool Enabled { get; init; }
    public string? UnavailableReason { get; init; }
    public bool Dangerous { get; init; }
    public string? ConfirmationText { get; init; }
    public string? ConfirmationChallenge { get; init; }
    public bool Pending { get; init; }
    public int? ExpectedDurationSeconds { get; init; }
    public ECostImpact CostImpact { get; init; }
    public string CostDescription { get; init; } = string.Empty;
}

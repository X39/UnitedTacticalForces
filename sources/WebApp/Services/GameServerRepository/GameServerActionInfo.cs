namespace X39.UnitedTacticalForces.WebApp.Services.GameServerRepository;

/// <summary>
/// Server-authoritative presentation and availability metadata for one operation.
/// </summary>
public sealed record GameServerActionInfo
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
    public string CostImpact { get; init; } = string.Empty;
    public string CostDescription { get; init; } = string.Empty;
}

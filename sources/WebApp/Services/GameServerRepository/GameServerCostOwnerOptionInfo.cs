namespace X39.UnitedTacticalForces.WebApp.Services.GameServerRepository;

public sealed record GameServerCostOwnerOptionInfo
{
    public Guid CostOwnerId { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public bool IsFrozen { get; init; }

    public bool IsCurrentUser { get; init; }
}

namespace X39.UnitedTacticalForces.WebApp.Services.GameServerRepository;

/// <summary>
/// Compact, authorized server item for notification-center surfaces.
/// </summary>
public sealed record GameServerNotificationInfo
{
    public Guid ServerId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public bool PossibleCost { get; init; }
    public bool CanOperate { get; init; }
    public GameServerActionInfo? CostStopAction { get; init; }
}

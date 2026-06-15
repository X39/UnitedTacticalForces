namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Compact authorized server item for the profile Notification Center.
/// </summary>
public sealed record GameServerNotificationDto
{
    public Guid ServerId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public bool PossibleCost { get; init; }
    public bool CanOperate { get; init; }
    public GameServerActionDto? CostStopAction { get; init; }
}

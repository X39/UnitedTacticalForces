namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// An automatic stop warning alert that was withdrawn because its stop was cancelled or already started.
/// </summary>
/// <param name="CostAlertId">The primary key of the withdrawn alert.</param>
/// <param name="GameServerId">The primary key of the server the alert belongs to.</param>
/// <param name="CostOwnerId">The primary key of the cost owner whose audience must drop the alert.</param>
internal sealed record ResolvedCostAlert(Guid CostAlertId, Guid GameServerId, Guid CostOwnerId);

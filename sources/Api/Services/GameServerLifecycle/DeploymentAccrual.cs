namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// The accrued cost of one evaluated deployment, shared with the update-stream publisher.
/// </summary>
/// <param name="DeploymentId">The primary key of the deployment.</param>
/// <param name="GameServerId">The primary key of the server the deployment belongs to.</param>
/// <param name="CostOwnerId">The primary key of the cost owner the deployment is billed to.</param>
/// <param name="IsCosting">
/// <see langword="true"/> while the deployment is unresolved and may still cost; <see langword="false"/> for a
/// deployment that was resolved within the lookback.
/// </param>
/// <param name="Result">The accrued cost the control plane calculated for the evaluation instant.</param>
/// <param name="Currency">The ISO currency code of the cost owner, in which the accrued amount is expressed.</param>
internal sealed record DeploymentAccrual(
    Guid DeploymentId,
    Guid GameServerId,
    Guid CostOwnerId,
    bool IsCosting,
    AccruedCostResult Result,
    string Currency);

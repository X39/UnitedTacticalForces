namespace X39.UnitedTacticalForces.Api.DTO.Payloads;

/// <summary>
/// Selects the payer used by future deployments of a stopped server.
/// </summary>
public sealed record GameServerCostOwnerUpdatePayload(Guid CostOwnerId);

namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>References a nested mod pack and an optional immutable revision.</summary>
/// <param name="DefinitionId">Nested definition identifier.</param>
/// <param name="PinnedRevisionId">Optional pinned revision identifier.</param>
public sealed record NormalizedModPackDependencyPayload(long DefinitionId, long? PinnedRevisionId);

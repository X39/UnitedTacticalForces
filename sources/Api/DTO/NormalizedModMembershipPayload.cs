namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>References a catalog mod and an optional immutable version.</summary>
/// <param name="ModId">Catalog mod identifier.</param>
/// <param name="PinnedVersionId">Optional pinned version identifier.</param>
public sealed record NormalizedModMembershipPayload(Guid ModId, Guid? PinnedVersionId);

namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Contains a filtered page of users eligible for cost-owner creation.
/// </summary>
public sealed record HostingCostOwnerUserCandidatePageDto
{
    /// <summary>
    /// Gets the matching candidates.
    /// </summary>
    public HostingCostOwnerUserCandidateDto[] Users { get; init; } = [];

    /// <summary>
    /// Gets the total matching candidate count.
    /// </summary>
    public long TotalCount { get; init; }
}

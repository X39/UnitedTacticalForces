namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// A filtered page of hosting cost owners.
/// </summary>
public sealed record HostingCostOwnerPageDto
{
    /// <summary>
    /// Cost owners in the requested page.
    /// </summary>
    public HostingCostOwnerDto[] Owners { get; init; } = [];

    /// <summary>
    /// Total number of cost owners matching the filter.
    /// </summary>
    public long TotalCount { get; init; }
}

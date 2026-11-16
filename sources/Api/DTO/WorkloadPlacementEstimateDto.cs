namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>Describes the current aggregate price of saved workload placement without accepting a deployment.</summary>
public sealed record WorkloadPlacementEstimateDto
{
    /// <summary>Gets the number of distinct hosts required by the placement.</summary>
    /// <value>The number of separately priced compute allocations.</value>
    public int HostCount { get; init; }

    /// <summary>Gets the aggregate hourly compute estimate.</summary>
    /// <value>The sum of compute charges for all planned hosts in EUR.</value>
    public decimal EstimatedComputeHourlyCostEur { get; init; }

    /// <summary>Gets the aggregate hourly external-storage estimate.</summary>
    /// <value>The sum of charges for the planned volumes in EUR.</value>
    public decimal EstimatedStorageHourlyCostEur { get; init; }

    /// <summary>Gets the combined hourly estimate for the next deployment.</summary>
    /// <value>Compute and external-storage charges in EUR; Start revalidates this estimate.</value>
    public decimal EstimatedHourlyCostEur { get; init; }

    /// <summary>Gets the combined monthly estimate when the provider supplies monthly compute pricing.</summary>
    /// <value>The aggregate monthly estimate in EUR, or null when monthly pricing is unavailable.</value>
    public decimal? EstimatedMonthlyCostEur { get; init; }
}

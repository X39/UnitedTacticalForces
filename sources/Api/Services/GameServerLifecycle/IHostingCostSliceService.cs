namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

internal interface IHostingCostSliceService
{
    Task ReserveInitialSliceAsync(
        Guid deploymentId,
        string providerIdentifier,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Answers, without writing anything, whether a cost owner could reserve the first provider-cost slice of a start
    /// now.
    /// </summary>
    /// <remarks>
    /// The calculation is the one <see cref="ReserveInitialSliceAsync" /> uses, but the executor's reservation stays
    /// authoritative: a start that still cannot reserve shows as a failed job.
    /// </remarks>
    /// <param name="costOwnerId">The cost owner that would pay for the start.</param>
    /// <param name="estimatedHourlyCostEur">The hourly estimate of the deployment, used when positive.</param>
    /// <param name="providerIdentifier">The provider whose configured rate applies without an estimate.</param>
    /// <param name="now">The instant the first slice would start at.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>
    /// A task resolving to <see langword="false" /> for a missing or frozen owner, a non-EUR owner, an unknown rate or
    /// when the slice would breach the minimum safety range; otherwise <see langword="true" />.
    /// </returns>
    Task<bool> CanReserveInitialSliceAsync(
        Guid costOwnerId,
        decimal? estimatedHourlyCostEur,
        string providerIdentifier,
        Instant now,
        CancellationToken cancellationToken = default);

    Task CommitInitialReservationAsync(
        Guid deploymentId,
        string providerIdentifier,
        CancellationToken cancellationToken = default);

    Task ReleaseInitialReservationAsync(
        Guid deploymentId,
        string reason,
        CancellationToken cancellationToken = default);

    Task AuthorizeNextSliceAsync(
        Guid deploymentId,
        string providerIdentifier,
        bool enforceAffordability,
        CancellationToken cancellationToken = default);
}
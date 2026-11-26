namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// One charged provider-cost slice of a deployment, reduced to the facts the accrual formula needs.
/// </summary>
/// <param name="StartsAt">The instant the slice starts covering provider cost.</param>
/// <param name="EndsAt">The instant the slice stops covering provider cost; it is later than <paramref name="StartsAt"/>.</param>
/// <param name="Amount">
/// The positive amount charged for the whole slice, that is the negated amount of the provider-cost ledger entry.
/// </param>
/// <param name="HourlyRate">
/// The hourly rate the slice was charged at, or <see langword="null"/> when the ledger entry recorded none; the
/// calculator then derives the rate from <paramref name="Amount"/> and the slice span.
/// </param>
internal readonly record struct AccruedCostSlice(
    Instant StartsAt,
    Instant EndsAt,
    decimal Amount,
    decimal? HourlyRate = null);

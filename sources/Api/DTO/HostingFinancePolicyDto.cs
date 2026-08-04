namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Effective installation-wide hosting affordability policy.
/// </summary>
public sealed record HostingFinancePolicyDto(
    decimal MinimumSafetyRangeEur,
    int ReservationSliceMinutes,
    Instant? TimeStampUpdated,
    Guid? UpdatedById);

namespace X39.UnitedTacticalForces.Api.DTO.Payloads;

/// <summary>
/// Updates the persisted installation-wide hosting affordability policy.
/// </summary>
public sealed record HostingFinancePolicyUpdatePayload(
    decimal MinimumSafetyRangeEur,
    int ReservationSliceMinutes);

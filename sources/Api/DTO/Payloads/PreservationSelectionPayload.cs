using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.DTO.Payloads;

/// <summary>
/// Adds or replaces one server-relative preservation selection.
/// </summary>
public sealed record PreservationSelectionPayload
{
    public string Path { get; init; } = string.Empty;
    public EPreservationScope Scope { get; init; }
    public EPreservationMode Mode { get; init; } = EPreservationMode.CreateOrUpdate;
}

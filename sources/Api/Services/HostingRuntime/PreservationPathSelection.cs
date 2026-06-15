using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

public sealed record PreservationPathSelection(
    Guid SelectionId,
    string RelativePath,
    EPreservationScope Scope,
    EPreservationMode Mode);
namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

public sealed record PreservationSelectionMatch(
    PreservationPathSelection Selection,
    string NormalizedSelectionPath);
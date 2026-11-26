using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.Services.ServerAutomation;

/// <summary>
/// The outcome of one idle clock transition.
/// </summary>
/// <param name="State">The clock state to persist; its idle start drives the displayed idle start.</param>
/// <param name="DisplayState">The idle state shown to users.</param>
public sealed record IdleClockResult(IdleClockState State, EAutomationIdleState DisplayState);

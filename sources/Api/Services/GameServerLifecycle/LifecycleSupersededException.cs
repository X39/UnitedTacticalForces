namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Execution was durably superseded by a stop/termination barrier and must not publish stale state.
/// </summary>
internal sealed class LifecycleSupersededException(string message) : OperationCanceledException(message);
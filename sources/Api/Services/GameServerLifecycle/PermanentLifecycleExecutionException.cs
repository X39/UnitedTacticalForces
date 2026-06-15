namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// An execution failure which cannot be corrected by retrying the same persisted request.
/// </summary>
internal sealed class PermanentLifecycleExecutionException(string message) : LifecycleExecutionException(message);
namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

internal class LifecycleExecutionException(string message, string? failureCode = null) : Exception(message)
{
    public string? FailureCode { get; } = failureCode;
}

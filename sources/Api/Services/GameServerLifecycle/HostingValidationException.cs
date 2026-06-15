namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

public sealed class HostingValidationException(string message) : ArgumentException(message);

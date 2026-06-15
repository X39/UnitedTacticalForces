namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

public sealed class HostingNotFoundException(string message) : KeyNotFoundException(message);

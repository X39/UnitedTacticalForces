namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

public sealed class HostingConflictException(string message) : InvalidOperationException(message);

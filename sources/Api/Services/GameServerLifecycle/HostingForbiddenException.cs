namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

public sealed class HostingForbiddenException(string message) : UnauthorizedAccessException(message);

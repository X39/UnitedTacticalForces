namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

public sealed class LiveWorkerFileAccessException(string message) : InvalidOperationException(message);
namespace X39.UnitedTacticalForces.Api.Services.WorkerProtocol;

internal sealed class WorkerProtocolConflictException(string message) : InvalidOperationException(message);

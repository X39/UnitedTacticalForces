using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.CloudWorker;

public sealed record WorkerRegistrationResult(
    string WorkerCredential,
    WorkerRegistrationResponse Response);
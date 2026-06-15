using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.CloudWorker;

public sealed record WorkerCredentialRotationResult(
    string WorkerCredential,
    WorkerCredentialRotationResponse Response);
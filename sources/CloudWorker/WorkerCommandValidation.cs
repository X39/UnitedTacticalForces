using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.CloudWorker;

internal static class WorkerCommandValidation
{
    public static void Validate(this WorkerCommandClaimResponse command, CloudWorkerConnectionConfig connection)
    {
        if (command.CommandId == Guid.Empty)
            throw new InvalidOperationException("Claimed command identity is empty.");
        if (command.DeploymentId != connection.DeploymentId || command.WorkerId != connection.WorkerId)
            throw new InvalidOperationException("Claimed command is not attributed to this worker deployment.");
        if (command.Sequence <= 0 || command.Attempt < 0 || string.IsNullOrWhiteSpace(command.ActionIdentifier))
            throw new InvalidOperationException("Claimed command metadata is invalid.");
    }
}
using System.Diagnostics;

namespace X39.UnitedTacticalForces.CloudWorker;

public interface IProcessCommandRunner
{
    Task<ProcessCommandResult> RunAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken);
}
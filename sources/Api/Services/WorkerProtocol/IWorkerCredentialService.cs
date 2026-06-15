using X39.UnitedTacticalForces.Database.Hosting;

namespace X39.UnitedTacticalForces.Api.Services.WorkerProtocol;

/// <summary>
/// Generates high-entropy worker secrets while persisting only their one-way hashes.
/// </summary>
public interface IWorkerCredentialService
{
    WorkerBootstrapConfiguration IssueBootstrapCredential(LifecycleWorker worker);

    bool VerifyBootstrapCredential(LifecycleWorker worker, string credential, Instant now);

    WorkerCredentialIssue IssuePendingWorkerCredential(LifecycleWorker worker, Instant now);

    bool VerifyAndPromoteWorkerCredential(LifecycleWorker worker, string credential, Instant now);

    void RevokeWorkerCredential(LifecycleWorker worker, Instant now);
}

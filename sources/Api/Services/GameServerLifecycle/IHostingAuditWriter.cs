using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Adds append-only hosting evidence to the caller's database transaction.
/// </summary>
public interface IHostingAuditWriter
{
    void Add(
        DatabaseContext dbContext,
        EHostingAuditEventKind kind,
        string summary,
        Guid? actorId = null,
        Guid? serverId = null,
        Guid? deploymentId = null,
        Guid? lifecycleJobId = null,
        Guid? workerId = null,
        Guid? costOwnerId = null,
        string? details = null,
        Guid? workerCommandId = null,
        ECostImpact costImpact = ECostImpact.NoCostImpact);
}

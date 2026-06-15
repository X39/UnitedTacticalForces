using System.Text.Json;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

[Scoped<HostingAuditWriter, IHostingAuditWriter>]
internal sealed class HostingAuditWriter : IHostingAuditWriter
{
    public void Add(
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
        ECostImpact costImpact = ECostImpact.NoCostImpact)
    {
        dbContext.HostingAuditRecords.Add(new HostingAuditRecord
        {
            PrimaryKey     = Guid.NewGuid(),
            Kind           = kind,
            TimeStampCreated = NodaSystemClock.Instance.GetCurrentInstant(),
            ActorFk        = actorId,
            GameServerFk   = serverId,
            DeploymentFk   = deploymentId,
            LifecycleJobFk = lifecycleJobId,
            WorkerFk       = workerId,
            WorkerCommandFk = workerCommandId,
            CostOwnerFk    = costOwnerId,
            CostImpact     = costImpact,
            Summary        = HostingFailureSanitizer.Sanitize(summary, 512),
            Details        = details is null
                ? "{}"
                : JsonSerializer.Serialize(new { detail = HostingFailureSanitizer.Sanitize(details, 2048) }),
        });
    }
}

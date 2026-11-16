using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace Api.Tests.GameServerHosting;

/// <summary>Completes persisted commands as a deterministic worker while recording lifecycle dispatch order.</summary>
internal sealed class ReadinessWorkerCommandInterceptor : SaveChangesInterceptor
{
    /// <summary>Gets commands in their original durable dispatch order.</summary>
    /// <value>The command entities observed when first inserted.</value>
    public List<WorkerCommand> Commands { get; } = [];

    /// <summary>Gets or sets whether readiness should fail while process commands succeed.</summary>
    /// <value>Whether to emulate a connected process that never achieves application readiness.</value>
    public bool FailReadiness { get; set; }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        foreach (var entry in eventData.Context!.ChangeTracker.Entries<WorkerCommand>()
                     .Where(entry => entry.State == EntityState.Added).ToArray())
        {
            var command = entry.Entity;
            Commands.Add(command);
            var failed = FailReadiness && command.ActionIdentifier == "verify-readiness";
            command.Status = failed ? EWorkerCommandStatus.Failed : EWorkerCommandStatus.Succeeded;
            command.Stage = failed ? "readiness-timeout" : "completed";
            command.ErrorCode = failed ? "arma-hc-not-joined" : null;
            command.ErrorMessage = failed ? "The assigned Headless Client did not join." : null;
            command.TimeStampCompleted = NodaTime.SystemClock.Instance.GetCurrentInstant();
            if (command.Worker is { } worker && command.ActionIdentifier is "start" or "restart")
            {
                worker.ProcessId = 1000 + Commands.Count;
                worker.ProcessStartedAt = command.TimeStampCompleted;
            }
        }
        return ValueTask.FromResult(result);
    }
}

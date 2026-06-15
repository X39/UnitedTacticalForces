using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.WorkerProtocol;
using Xunit;

namespace Api.Tests.GameServerHosting;

public sealed class WorkerCredentialServiceTests
{
    [Fact]
    public void BootstrapCredential_RemainsRetryableUntilPendingCredentialProvesReceipt()
    {
        var worker = Worker();
        var service = CreateService();

        var bootstrap = service.IssueBootstrapCredential(worker);
        var beforeExpiry = Instant.FromDateTimeOffset(bootstrap.ExpiresAt) - Duration.FromSeconds(1);

        Assert.Equal(worker.PrimaryKey, bootstrap.WorkerId);
        Assert.Equal(worker.DeploymentFk, bootstrap.DeploymentId);
        Assert.Equal(64, bootstrap.BootstrapCredential.Length);
        Assert.Equal(64, worker.BootstrapCredentialHash?.Length);
        Assert.NotEqual(bootstrap.BootstrapCredential, worker.BootstrapCredentialHash);
        Assert.DoesNotContain(bootstrap.BootstrapCredential, bootstrap.RegistrationPath, StringComparison.Ordinal);
        Assert.True(service.VerifyBootstrapCredential(worker, bootstrap.BootstrapCredential, beforeExpiry));
        Assert.False(service.VerifyBootstrapCredential(worker, bootstrap.BootstrapCredential + "0", beforeExpiry));
        Assert.False(service.VerifyBootstrapCredential(
            worker,
            bootstrap.BootstrapCredential,
            Instant.FromDateTimeOffset(bootstrap.ExpiresAt)));

        worker.BootstrapCredentialConsumedAt = beforeExpiry;
        var pending = service.IssuePendingWorkerCredential(worker, beforeExpiry);

        Assert.True(service.VerifyBootstrapCredential(worker, bootstrap.BootstrapCredential, beforeExpiry));
        Assert.True(service.VerifyAndPromoteWorkerCredential(worker, pending.Credential, beforeExpiry));
        Assert.Null(worker.BootstrapCredentialHash);
        Assert.False(service.VerifyBootstrapCredential(worker, bootstrap.BootstrapCredential, beforeExpiry));
    }

    [Fact]
    public void Rotation_KeepsCurrentValidUntilPendingAuthenticatesThenRevokesAllMaterial()
    {
        var worker = Worker();
        var service = CreateService();
        var now = Instant.FromUnixTimeSeconds(1_800_000_000);

        var initial = service.IssuePendingWorkerCredential(worker, now);
        Assert.True(service.VerifyAndPromoteWorkerCredential(worker, initial.Credential, now));
        var currentHash = worker.WorkerCredentialHash;
        var replacement = service.IssuePendingWorkerCredential(worker, now + Duration.FromMinutes(1));

        Assert.Equal(currentHash, worker.WorkerCredentialHash);
        Assert.NotEqual(replacement.Credential, worker.PendingWorkerCredentialHash);
        Assert.True(service.VerifyAndPromoteWorkerCredential(worker, initial.Credential, now));
        Assert.True(service.VerifyAndPromoteWorkerCredential(
            worker,
            replacement.Credential,
            now + Duration.FromMinutes(1)));
        Assert.Null(worker.PendingWorkerCredentialHash);
        Assert.False(service.VerifyAndPromoteWorkerCredential(worker, initial.Credential, now));
        Assert.False(service.VerifyAndPromoteWorkerCredential(worker, "incorrect", now));
        Assert.False(service.VerifyAndPromoteWorkerCredential(
            worker,
            replacement.Credential,
            Instant.FromDateTimeOffset(replacement.ExpiresAt)));

        service.RevokeWorkerCredential(worker, now + Duration.FromMinutes(1));

        Assert.Null(worker.WorkerCredentialHash);
        Assert.Null(worker.PendingWorkerCredentialHash);
        Assert.Null(worker.BootstrapCredentialHash);
        Assert.NotNull(worker.WorkerCredentialRevokedAt);
        Assert.False(service.VerifyAndPromoteWorkerCredential(worker, replacement.Credential, now));
    }

    private static WorkerCredentialService CreateService()
        => new(Options.Create(new LifecycleWorkerOptions
        {
            RegistrationGrace = TimeSpan.FromMinutes(2),
            WorkerCredentialLifetime = TimeSpan.FromHours(1),
        }));

    private static LifecycleWorker Worker()
        => new()
        {
            PrimaryKey = Guid.NewGuid(),
            DeploymentFk = Guid.NewGuid(),
            TimeStampCreated = Instant.FromUnixTimeSeconds(1_700_000_000),
            TimeStampUpdated = Instant.FromUnixTimeSeconds(1_700_000_000),
        };
}

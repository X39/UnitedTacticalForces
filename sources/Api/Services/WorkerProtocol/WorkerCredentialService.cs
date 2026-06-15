using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.WorkerProtocol;

[Singleton<WorkerCredentialService, IWorkerCredentialService>]
internal sealed class WorkerCredentialService(IOptions<LifecycleWorkerOptions> options) : IWorkerCredentialService
{
    private const int CredentialByteCount = 32;
    private const int MaximumAcceptedCredentialLength = 512;
    private readonly LifecycleWorkerOptions _options = options.Value;

    public WorkerBootstrapConfiguration IssueBootstrapCredential(LifecycleWorker worker)
    {
        EnsurePositive(
            _options.BootstrapCredentialLifetime,
            nameof(LifecycleWorkerOptions.BootstrapCredentialLifetime));
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var expiresAt = now + Duration.FromTimeSpan(_options.BootstrapCredentialLifetime);
        var credential = GenerateCredential();

        if (worker.WorkerCredentialHash is not null && worker.WorkerCredentialRevokedAt is null)
            worker.WorkerCredentialRevokedAt = now;
        worker.WorkerCredentialHash = null;
        worker.WorkerCredentialActivatedAt = null;
        ClearPendingWorkerCredential(worker);
        worker.BootstrapCredentialHash = HashCredential(credential);
        worker.BootstrapCredentialExpiresAt = expiresAt;
        worker.BootstrapCredentialConsumedAt = null;
        worker.TimeStampUpdated = now;

        return new WorkerBootstrapConfiguration(
            worker.PrimaryKey,
            worker.DeploymentFk,
            credential,
            expiresAt.ToDateTimeOffset(),
            $"/lifecycle-workers/{worker.PrimaryKey:D}/register");
    }

    public bool VerifyBootstrapCredential(LifecycleWorker worker, string credential, Instant now)
        => worker.BootstrapCredentialExpiresAt is { } expiresAt
           && expiresAt > now
           && VerifyHash(worker.BootstrapCredentialHash, credential);

    public WorkerCredentialIssue IssuePendingWorkerCredential(LifecycleWorker worker, Instant now)
    {
        EnsurePositive(_options.WorkerCredentialLifetime, nameof(LifecycleWorkerOptions.WorkerCredentialLifetime));
        var expiresAt = now + Duration.FromTimeSpan(_options.WorkerCredentialLifetime);
        var credential = GenerateCredential();
        worker.PendingWorkerCredentialHash = HashCredential(credential);
        worker.PendingWorkerCredentialIssuedAt = now;
        worker.PendingWorkerCredentialExpiresAt = expiresAt;
        worker.TimeStampUpdated = now;
        return new WorkerCredentialIssue(credential, expiresAt.ToDateTimeOffset());
    }

    public bool VerifyAndPromoteWorkerCredential(LifecycleWorker worker, string credential, Instant now)
    {
        if (worker.WorkerCredentialRevokedAt is null
            && worker.WorkerCredentialActivatedAt is not null
            && worker.WorkerCredentialExpiresAt is { } currentExpiresAt
            && currentExpiresAt > now
            && VerifyHash(worker.WorkerCredentialHash, credential))
        {
            return true;
        }

        if (worker.PendingWorkerCredentialExpiresAt is not { } pendingExpiresAt
            || pendingExpiresAt <= now
            || !VerifyHash(worker.PendingWorkerCredentialHash, credential))
        {
            return false;
        }

        worker.WorkerCredentialHash = worker.PendingWorkerCredentialHash;
        worker.WorkerCredentialIssuedAt = worker.PendingWorkerCredentialIssuedAt;
        worker.WorkerCredentialExpiresAt = pendingExpiresAt;
        worker.WorkerCredentialActivatedAt = now;
        worker.WorkerCredentialRevokedAt = null;
        ClearPendingWorkerCredential(worker);

        // A successful request with the pending credential proves that registration material
        // reached the intended worker. Only now is the retryable bootstrap retired.
        worker.BootstrapCredentialHash = null;
        worker.TimeStampUpdated = now;
        return true;
    }

    public void RevokeWorkerCredential(LifecycleWorker worker, Instant now)
    {
        if (worker.WorkerCredentialIssuedAt is not null)
            worker.WorkerCredentialRevokedAt ??= now;
        worker.WorkerCredentialHash = null;
        worker.WorkerCredentialActivatedAt = null;
        ClearPendingWorkerCredential(worker);
        worker.BootstrapCredentialHash = null;
        worker.BootstrapCredentialExpiresAt = null;
        worker.BootstrapCredentialConsumedAt = null;
        worker.TimeStampUpdated = now;
    }

    private static void ClearPendingWorkerCredential(LifecycleWorker worker)
    {
        worker.PendingWorkerCredentialHash = null;
        worker.PendingWorkerCredentialIssuedAt = null;
        worker.PendingWorkerCredentialExpiresAt = null;
    }

    private static string GenerateCredential()
        => Convert.ToHexString(RandomNumberGenerator.GetBytes(CredentialByteCount));

    private static string HashCredential(string credential)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(credential)));

    private static bool VerifyHash(string? expectedHash, string credential)
    {
        if (expectedHash is null
            || credential.Length is 0 or > MaximumAcceptedCredentialLength
            || expectedHash.Length != SHA256.HashSizeInBytes * 2)
        {
            return false;
        }

        byte[] expected;
        try
        {
            expected = Convert.FromHexString(expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }
        if (expected.Length != SHA256.HashSizeInBytes)
            return false;

        Span<byte> actual = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(Encoding.UTF8.GetBytes(credential), actual);
        var verified = CryptographicOperations.FixedTimeEquals(expected, actual);
        CryptographicOperations.ZeroMemory(expected);
        return verified;
    }

    private static void EnsurePositive(TimeSpan value, string optionName)
    {
        if (value <= TimeSpan.Zero)
            throw new InvalidOperationException($"{optionName} must be greater than zero.");
    }
}

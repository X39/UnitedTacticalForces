using System.Text.Json;
using System.Text.Json.Serialization;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>
/// Short-lived provider bootstrap material mounted into one dedicated worker.
/// </summary>
public sealed record CloudWorkerBootstrapConfig : CloudWorkerConnectionConfig
{
    [JsonPropertyName("bootstrapCredential")]
    public string BootstrapCredential { get; init; } = string.Empty;

    [JsonPropertyName("expiresAt")]
    public DateTimeOffset ExpiresAt { get; init; }

    public static async Task<CloudWorkerBootstrapConfig> LoadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(path);
        RestrictedFilePermissions.EnsureOwnerOnlyReadable(fullPath, "Worker bootstrap configuration");
        await using var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var config = await JsonSerializer.DeserializeAsync<CloudWorkerBootstrapConfig>(
                stream,
                CloudWorkerJson.Options,
                cancellationToken)
            .ConfigureAwait(false)
                     ?? throw new InvalidOperationException("Worker bootstrap configuration is empty.");
        config.Validate();
        return config;
    }

    public override void Validate()
    {
        base.Validate();
        if (string.IsNullOrWhiteSpace(BootstrapCredential) || BootstrapCredential.Length > 512)
            throw new InvalidOperationException("Worker bootstrap credential is missing.");
        if (ExpiresAt == default)
            throw new InvalidOperationException("Worker bootstrap expiry is missing.");
    }

    public CloudWorkerConnectionConfig ToConnectionConfig()
        => new()
        {
            ApiBaseUrl = ApiBaseUrl,
            WorkerId = WorkerId,
            DeploymentId = DeploymentId,
            ServerId = ServerId,
            GameIdentifier = GameIdentifier,
            WorkerApiPath = WorkerApiPath,
        };
}
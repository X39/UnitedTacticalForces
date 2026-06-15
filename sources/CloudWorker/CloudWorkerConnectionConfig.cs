using System.Text.Json.Serialization;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>
/// Non-secret identity and control-plane location persisted for one dedicated worker.
/// </summary>
public record CloudWorkerConnectionConfig
{
    [JsonPropertyName("apiBaseUrl")]
    public string ApiBaseUrl { get; init; } = string.Empty;

    [JsonPropertyName("workerId")]
    public Guid WorkerId { get; init; }

    [JsonPropertyName("deploymentId")]
    public Guid DeploymentId { get; init; }

    [JsonPropertyName("serverId")]
    public Guid ServerId { get; init; }

    [JsonPropertyName("gameIdentifier")]
    public string GameIdentifier { get; init; } = string.Empty;

    [JsonPropertyName("workerApiPath")]
    public string WorkerApiPath { get; init; } = "lifecycle-workers";

    public virtual void Validate()
    {
        if (!Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("https" or "http")
            || string.IsNullOrEmpty(uri.Host)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException("Worker control-plane API base URL is invalid.");
        }
        if (WorkerId == Guid.Empty || DeploymentId == Guid.Empty || ServerId == Guid.Empty)
            throw new InvalidOperationException("Worker connection identifiers must be non-empty GUIDs.");
        if (string.IsNullOrWhiteSpace(GameIdentifier) || GameIdentifier.Length > 128)
            throw new InvalidOperationException("Worker connection game identifier is missing.");
        if (string.IsNullOrWhiteSpace(WorkerApiPath)
            || WorkerApiPath.Length > 256
            || WorkerApiPath.Contains("..", StringComparison.Ordinal)
            || WorkerApiPath.IndexOfAny(['?', '#', '\\', '\r', '\n', '\0']) >= 0
            || Uri.TryCreate(WorkerApiPath, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("Worker control-plane API path is invalid.");
        }
    }
}
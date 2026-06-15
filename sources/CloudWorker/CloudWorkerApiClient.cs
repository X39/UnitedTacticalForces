using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>
/// Outbound-only client for the deployment-scoped worker control surface.
/// </summary>
public sealed class CloudWorkerApiClient(HttpClient httpClient) : ICloudWorkerApiClient
{
    public const string WorkerCredentialHeader = WorkerProtocolHeaders.WorkerCredential;

    public async Task<WorkerRegistrationResult> RegisterAsync(
        CloudWorkerBootstrapConfig bootstrap,
        WorkerRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        using var message = CreateRequest(
            HttpMethod.Post,
            bootstrap,
            $"{WorkerPath(bootstrap)}/register",
            bootstrap.BootstrapCredential);
        message.Content = JsonContent.Create(request, options: CloudWorkerJson.Options);
        using var response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, "registration", cancellationToken).ConfigureAwait(false);
        var workerCredential = ReadCredentialHeader(response, "Worker registration");
        var body = await response.Content
            .ReadFromJsonAsync<WorkerRegistrationResponse>(CloudWorkerJson.Options, cancellationToken)
            .ConfigureAwait(false)
                   ?? throw new InvalidOperationException("Worker registration returned an empty response.");
        if (body.WorkerId != bootstrap.WorkerId || body.DeploymentId != bootstrap.DeploymentId)
            throw new InvalidOperationException("Worker registration response attribution does not match bootstrap.");
        if (body.CredentialExpiresAt <= DateTimeOffset.UtcNow || body.HeartbeatIntervalSeconds <= 0)
            throw new InvalidOperationException("Worker registration returned invalid credential metadata.");
        return new WorkerRegistrationResult(workerCredential, body);
    }

    public async Task<WorkerCredentialRotationResult> RotateCredentialAsync(
        CloudWorkerConnectionConfig connection,
        string workerCredential,
        CancellationToken cancellationToken)
    {
        using var message = CreateRequest(
            HttpMethod.Post,
            connection,
            $"{WorkerPath(connection)}/rotate-credential",
            workerCredential);
        message.Content = JsonContent.Create(
            new WorkerCredentialRotationRequest { DeploymentId = connection.DeploymentId },
            options: CloudWorkerJson.Options);
        using var response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, "credential rotation", cancellationToken).ConfigureAwait(false);
        var replacementCredential = ReadCredentialHeader(response, "Worker credential rotation");
        var body = await response.Content
            .ReadFromJsonAsync<WorkerCredentialRotationResponse>(CloudWorkerJson.Options, cancellationToken)
            .ConfigureAwait(false)
                   ?? throw new InvalidOperationException("Worker credential rotation returned an empty response.");
        if (body.WorkerId != connection.WorkerId || body.DeploymentId != connection.DeploymentId)
            throw new InvalidOperationException("Worker credential rotation response attribution does not match runtime.");
        if (body.CredentialExpiresAt <= DateTimeOffset.UtcNow)
            throw new InvalidOperationException("Worker credential rotation returned invalid expiration metadata.");
        return new WorkerCredentialRotationResult(replacementCredential, body);
    }

    public async Task HeartbeatAsync(
        CloudWorkerConnectionConfig connection,
        string workerCredential,
        WorkerHeartbeatRequest request,
        CancellationToken cancellationToken)
    {
        using var message = CreateRequest(
            HttpMethod.Post,
            connection,
            $"{WorkerPath(connection)}/heartbeat",
            workerCredential);
        message.Content = JsonContent.Create(request, options: CloudWorkerJson.Options);
        using var response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, "heartbeat", cancellationToken).ConfigureAwait(false);
    }

    public async Task<WorkerCommandClaimResponse?> ClaimNextAsync(
        CloudWorkerConnectionConfig connection,
        string workerCredential,
        CancellationToken cancellationToken)
    {
        using var message = CreateRequest(
            HttpMethod.Post,
            connection,
            $"{WorkerPath(connection)}/commands/claim",
            workerCredential);
        message.Content = JsonContent.Create(
            new WorkerCommandClaimRequest { DeploymentId = connection.DeploymentId },
            options: CloudWorkerJson.Options);
        using var response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NoContent)
            return null;
        await EnsureSuccessAsync(response, "command claim", cancellationToken).ConfigureAwait(false);
        return await response.Content
            .ReadFromJsonAsync<WorkerCommandClaimResponse>(CloudWorkerJson.Options, cancellationToken)
            .ConfigureAwait(false)
               ?? throw new InvalidOperationException("Worker command claim returned an empty response.");
    }

    public async Task<bool> ReportCommandAsync(
        CloudWorkerConnectionConfig connection,
        string workerCredential,
        Guid commandId,
        WorkerCommandReportRequest request,
        CancellationToken cancellationToken)
    {
        var statePath = request.State.ToString().ToLowerInvariant();
        using var message = CreateRequest(
            HttpMethod.Post,
            connection,
            $"{WorkerPath(connection)}/commands/{commandId:D}/{statePath}",
            workerCredential);
        message.Content = request.State switch
        {
            EWorkerCommandReportState.Received => JsonContent.Create(
                new WorkerCommandStageRequest { DeploymentId = request.DeploymentId, Stage = request.Stage },
                options: CloudWorkerJson.Options),
            EWorkerCommandReportState.Running or EWorkerCommandReportState.Succeeded => JsonContent.Create(
                new WorkerCommandProcessRequest
                {
                    DeploymentId = request.DeploymentId,
                    Stage = request.Stage,
                    ProcessId = request.ProcessId,
                    ProcessStartedAt = request.ProcessStartedAt,
                    Output = request.Output,
                },
                options: CloudWorkerJson.Options),
            EWorkerCommandReportState.Failed => JsonContent.Create(
                new WorkerCommandFailureRequest
                {
                    DeploymentId = request.DeploymentId,
                    Stage = request.Stage,
                    ErrorCode = request.ErrorCode ?? "worker-command-failed",
                    ErrorMessage = request.ErrorMessage ?? "Worker command failed.",
                    Output = request.Output,
                },
                options: CloudWorkerJson.Options),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.State, "Unknown command report state."),
        };
        using var response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Conflict)
            return false;
        await EnsureSuccessAsync(response, $"command {statePath}", cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        CloudWorkerConnectionConfig connection,
        string relativePath,
        string credential)
    {
        var request = new HttpRequestMessage(method, BuildUri(connection.ApiBaseUrl, relativePath));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static string WorkerPath(CloudWorkerConnectionConfig connection)
        => $"{connection.WorkerApiPath.Trim('/')}/{connection.WorkerId:D}";

    private static string ReadCredentialHeader(HttpResponseMessage response, string operation)
    {
        if (!response.Headers.TryGetValues(WorkerCredentialHeader, out var credentialValues))
            throw new InvalidOperationException($"{operation} did not issue a worker credential header.");
        var issuedCredentials = credentialValues.ToArray();
        if (issuedCredentials is not [{ Length: > 0 and <= 512 } credential])
            throw new InvalidOperationException($"{operation} issued invalid worker credential metadata.");
        return credential;
    }

    private static Uri BuildUri(string baseUrl, string relativePath)
        => new(new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/"), relativePath.TrimStart('/'));

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;
        var detail = string.Empty;
        if (response.Content.Headers.ContentType?.MediaType == "application/problem+json")
        {
            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                if (document.RootElement.TryGetProperty("title", out var title))
                    detail = $" ({WorkerSecretRedactor.Redact(title.GetString())})";
            }
            catch (JsonException)
            {
            }
        }
        throw new HttpRequestException(
            $"Worker {operation} failed with HTTP {(int)response.StatusCode}{detail}.",
            null,
            response.StatusCode);
    }

}

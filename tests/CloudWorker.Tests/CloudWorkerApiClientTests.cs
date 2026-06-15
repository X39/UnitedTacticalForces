using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using X39.UnitedTacticalForces.CloudWorker;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using Xunit;

namespace CloudWorker.Tests;

public class CloudWorkerApiClientTests
{
    [Fact]
    public async Task Requests_MatchSharedWorkerProtocolRoutesBodiesAndHeaders()
    {
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var command = FlatFileWorkerCommandJournalTests.CreateCommand(
            bootstrap.WorkerId,
            bootstrap.DeploymentId,
            bootstrap.ServerId);
        var requests = new List<RecordedRequest>();
        var handler = new RecordingHandler(async request =>
        {
            requests.Add(await RecordedRequest.CreateAsync(request));
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/register", StringComparison.Ordinal))
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new WorkerRegistrationResponse
                    {
                        WorkerId = bootstrap.WorkerId,
                        DeploymentId = bootstrap.DeploymentId,
                        CredentialExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
                        HeartbeatIntervalSeconds = 15,
                    }, options: CloudWorkerJson.Options),
                };
                response.Headers.Add(CloudWorkerApiClient.WorkerCredentialHeader, "worker-token");
                return response;
            }
            if (path.EndsWith("/rotate-credential", StringComparison.Ordinal))
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new WorkerCredentialRotationResponse
                    {
                        WorkerId = bootstrap.WorkerId,
                        DeploymentId = bootstrap.DeploymentId,
                        CredentialExpiresAt = DateTimeOffset.UtcNow.AddHours(2),
                    }, options: CloudWorkerJson.Options),
                };
                response.Headers.Add(CloudWorkerApiClient.WorkerCredentialHeader, "replacement-worker-token");
                return response;
            }
            if (path.EndsWith("/commands/claim", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(command, options: CloudWorkerJson.Options),
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        using var httpClient = new HttpClient(handler);
        var client = new CloudWorkerApiClient(httpClient);

        var registration = await client.RegisterAsync(
            bootstrap,
            new WorkerRegistrationRequest { DeploymentId = bootstrap.DeploymentId, Identity = "cloud-worker" },
            CancellationToken.None);
        await client.HeartbeatAsync(
            bootstrap,
            registration.WorkerCredential,
            new WorkerHeartbeatRequest { DeploymentId = bootstrap.DeploymentId, Stage = "idle" },
            CancellationToken.None);
        var claimed = await client.ClaimNextAsync(bootstrap, registration.WorkerCredential, CancellationToken.None);
        var reportAccepted = await client.ReportCommandAsync(
            bootstrap,
            registration.WorkerCredential,
            command.CommandId,
            new WorkerCommandReportRequest(
                bootstrap.DeploymentId,
                EWorkerCommandReportState.Succeeded,
                "logs-collected",
                DateTimeOffset.UtcNow,
                null,
                null,
                "collected-log-output",
                42,
                DateTimeOffset.UtcNow),
            CancellationToken.None);
        var rotation = await client.RotateCredentialAsync(
            bootstrap,
            registration.WorkerCredential,
            CancellationToken.None);

        Assert.Equal("worker-token", registration.WorkerCredential);
        Assert.True(reportAccepted);
        Assert.Equal(command.CommandId, claimed?.CommandId);
        Assert.Equal(
            $"/api/lifecycle-workers/{bootstrap.WorkerId:D}/register",
            requests[0].Path);
        Assert.Equal("Bearer bootstrap-value-not-for-logs", requests[0].Authorization);
        Assert.Equal("worker-token", requests[1].BearerToken);
        Assert.Contains("\"deploymentId\"", requests[2].Body, StringComparison.Ordinal);
        Assert.Equal(
            $"/api/lifecycle-workers/{bootstrap.WorkerId:D}/commands/{command.CommandId:D}/succeeded",
            requests[3].Path);
        Assert.Contains("\"processId\":42", requests[3].Body, StringComparison.Ordinal);
        Assert.Contains("\"output\":\"collected-log-output\"", requests[3].Body, StringComparison.Ordinal);
        Assert.Equal("replacement-worker-token", rotation.WorkerCredential);
        Assert.Equal(
            $"/api/lifecycle-workers/{bootstrap.WorkerId:D}/rotate-credential",
            requests[4].Path);
        Assert.Equal("worker-token", requests[4].BearerToken);
        Assert.Contains("\"deploymentId\"", requests[4].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SupersededCommandReport_ReturnsFalseOnConflict()
    {
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        using var httpClient = new HttpClient(new RecordingHandler(
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict))));
        var client = new CloudWorkerApiClient(httpClient);

        var accepted = await client.ReportCommandAsync(
            bootstrap,
            "worker-token",
            Guid.NewGuid(),
            new WorkerCommandReportRequest(
                bootstrap.DeploymentId,
                EWorkerCommandReportState.Running,
                "running",
                DateTimeOffset.UtcNow,
                null,
                null,
                null,
                null,
                null),
            CancellationToken.None);

        Assert.False(accepted);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handle)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => handle(request);
    }

    private sealed record RecordedRequest(string Path, string Authorization, string BearerToken, string Body)
    {
        public static async Task<RecordedRequest> CreateAsync(HttpRequestMessage request)
        {
            var authorization = request.Headers.Authorization;
            return new RecordedRequest(
                request.RequestUri!.AbsolutePath,
                authorization?.ToString() ?? string.Empty,
                authorization?.Parameter ?? string.Empty,
                request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync());
        }
    }
}

using System.Net.WebSockets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Api.Controllers;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.WorkerProtocol;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using Xunit;

namespace Api.Tests.GameServerHosting;

public sealed class LifecycleWorkersControllerTests
{
    [Fact]
    public async Task Register_ReturnsWorkerCredentialOnlyInNoStoreHeader()
    {
        const string workerCredential = "worker-secret-value";
        var workerId = Guid.NewGuid();
        var deploymentId = Guid.NewGuid();
        var response = new WorkerRegistrationResponse
        {
            WorkerId = workerId,
            DeploymentId = deploymentId,
            CredentialExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            HeartbeatIntervalSeconds = 10,
        };
        var protocol = new RegistrationProtocolStub(new WorkerRegistrationResult(response, workerCredential));
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = "Bearer bootstrap-secret";
        var controller = new LifecycleWorkersController(protocol)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
        };

        var result = await controller.RegisterAsync(
            workerId,
            new WorkerRegistrationRequest
            {
                DeploymentId = deploymentId,
                Identity = "worker-agent",
            },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(response, ok.Value);
        Assert.Equal(workerCredential, httpContext.Response.Headers[WorkerProtocolHeaders.WorkerCredential]);
        Assert.Contains("no-store", httpContext.Response.Headers.CacheControl.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(workerCredential, System.Text.Json.JsonSerializer.Serialize(ok.Value), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Register_RejectsMissingBearerWithoutCallingProtocol()
    {
        var protocol = new RegistrationProtocolStub(null);
        var controller = new LifecycleWorkersController(protocol)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        var result = await controller.RegisterAsync(
            Guid.NewGuid(),
            new WorkerRegistrationRequest(),
            CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
        Assert.False(protocol.WasCalled);
    }

    [Fact]
    public async Task RotateCredential_ReturnsSecretOnlyInNoStoreHeader()
    {
        const string replacementCredential = "replacement-worker-secret";
        var workerId = Guid.NewGuid();
        var deploymentId = Guid.NewGuid();
        var response = new WorkerCredentialRotationResponse
        {
            WorkerId = workerId,
            DeploymentId = deploymentId,
            CredentialExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
        };
        var protocol = new RegistrationProtocolStub(
            null,
            new WorkerCredentialRotationResult(response, replacementCredential));
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = "Bearer current-worker-secret";
        var controller = new LifecycleWorkersController(protocol)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
        };

        var result = await controller.RotateCredentialAsync(
            workerId,
            new WorkerCredentialRotationRequest { DeploymentId = deploymentId },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(response, ok.Value);
        Assert.Equal(replacementCredential, httpContext.Response.Headers[WorkerProtocolHeaders.WorkerCredential]);
        Assert.Contains("no-store", httpContext.Response.Headers.CacheControl.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(
            replacementCredential,
            System.Text.Json.JsonSerializer.Serialize(ok.Value),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RotateCredential_RejectsMissingBearerWithoutCallingProtocol()
    {
        var protocol = new RegistrationProtocolStub(null);
        var controller = new LifecycleWorkersController(protocol)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        var result = await controller.RotateCredentialAsync(
            Guid.NewGuid(),
            new WorkerCredentialRotationRequest { DeploymentId = Guid.NewGuid() },
            CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
        Assert.False(protocol.WasCalled);
    }

    [Fact]
    public void Controller_IsExcludedFromBrowserFacingApiExplorer()
    {
        var attribute = Assert.Single(
            typeof(LifecycleWorkersController)
                .GetCustomAttributes(typeof(ApiExplorerSettingsAttribute), false)
                .Cast<ApiExplorerSettingsAttribute>());

        Assert.True(attribute.IgnoreApi);
    }

    [Fact]
    public async Task Channel_RejectsMissingBearerWithoutCallingProtocol()
    {
        var protocol = new RegistrationProtocolStub(null);
        var feature = new RecordingWebSocketFeature(isWebSocketRequest: true);
        var controller = CreateChannelController(protocol, feature, out var httpContext);

        await controller.ChannelAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new RecordingChannelHandler(),
            Options.Create(new LifecycleWorkerOptions()),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status401Unauthorized, httpContext.Response.StatusCode);
        Assert.False(protocol.WasCalled);
        Assert.False(feature.AcceptCalled);
    }

    [Fact]
    public async Task Channel_RejectsANonWebSocketRequestWithBadRequest()
    {
        var protocol = new RegistrationProtocolStub(null);
        var feature = new RecordingWebSocketFeature(isWebSocketRequest: false);
        var controller = CreateChannelController(protocol, feature, out var httpContext);
        httpContext.Request.Headers.Authorization = "Bearer worker-secret";

        await controller.ChannelAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new RecordingChannelHandler(),
            Options.Create(new LifecycleWorkerOptions()),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
        Assert.False(protocol.WasCalled);
        Assert.False(feature.AcceptCalled);
    }

    [Fact]
    public async Task Channel_RefusesARejectedCredentialBeforeAcceptingTheUpgrade()
    {
        var protocol = new RegistrationProtocolStub(null);
        var feature = new RecordingWebSocketFeature(isWebSocketRequest: true);
        var handler = new RecordingChannelHandler();
        var controller = CreateChannelController(protocol, feature, out var httpContext);
        httpContext.Request.Headers.Authorization = "Bearer rejected-secret";

        await controller.ChannelAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            handler,
            Options.Create(new LifecycleWorkerOptions()),
            CancellationToken.None);

        Assert.True(protocol.WasCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, httpContext.Response.StatusCode);
        Assert.False(feature.AcceptCalled);
        Assert.False(handler.WasRun);
    }

    [Fact]
    public async Task Channel_AcceptsWithTheConfiguredKeepAliveAndRunsTheHandlerWithTheBearerCredential()
    {
        var workerId = Guid.NewGuid();
        var deploymentId = Guid.NewGuid();
        var identity = new AuthenticatedWorker(workerId, deploymentId, Guid.NewGuid(), "game-server", 0);
        var protocol = new RegistrationProtocolStub(null) { Authenticated = identity };
        await using var pair = await LoopbackWebSocketPair.CreateAsync();
        var feature = new RecordingWebSocketFeature(isWebSocketRequest: true) { SocketToAccept = pair.Server };
        var handler = new RecordingChannelHandler();
        var controller = CreateChannelController(protocol, feature, out var httpContext);
        httpContext.Request.Headers.Authorization = "Bearer worker-secret";
        var options = new LifecycleWorkerOptions
        {
            ChannelKeepAliveInterval = TimeSpan.FromSeconds(7),
            ChannelKeepAliveTimeout = TimeSpan.FromSeconds(21),
        };

        await controller.ChannelAsync(workerId, deploymentId, handler, Options.Create(options), CancellationToken.None);

        Assert.True(feature.AcceptCalled);
        Assert.Equal(TimeSpan.FromSeconds(7), feature.AcceptContext!.KeepAliveInterval);
        Assert.Equal(TimeSpan.FromSeconds(21), feature.AcceptContext.KeepAliveTimeout);
        Assert.True(handler.WasRun);
        Assert.Equal(identity, handler.Worker);
        Assert.Equal("worker-secret", handler.Credential);
        Assert.Equal((workerId, deploymentId), protocol.LastAuthentication);
    }

    private static LifecycleWorkersController CreateChannelController(
        RegistrationProtocolStub protocol,
        RecordingWebSocketFeature feature,
        out DefaultHttpContext httpContext)
    {
        httpContext = new DefaultHttpContext();
        httpContext.Features.Set<IHttpWebSocketFeature>(feature);
        return new LifecycleWorkersController(protocol)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
        };
    }

    private sealed class RecordingWebSocketFeature(bool isWebSocketRequest) : IHttpWebSocketFeature
    {
        public bool IsWebSocketRequest { get; } = isWebSocketRequest;

        public bool AcceptCalled { get; private set; }

        public WebSocketAcceptContext? AcceptContext { get; private set; }

        public WebSocket? SocketToAccept { get; init; }

        public Task<WebSocket> AcceptAsync(WebSocketAcceptContext context)
        {
            AcceptCalled = true;
            AcceptContext = context;
            return Task.FromResult(SocketToAccept ?? throw new InvalidOperationException("Unexpected accept."));
        }
    }

    private sealed class RecordingChannelHandler : IWorkerChannelHandler
    {
        public bool WasRun { get; private set; }

        public AuthenticatedWorker? Worker { get; private set; }

        public string? Credential { get; private set; }

        public Task RunAsync(
            AuthenticatedWorker worker,
            string credential,
            WebSocket socket,
            CancellationToken cancellationToken)
        {
            WasRun = true;
            Worker = worker;
            Credential = credential;
            return Task.CompletedTask;
        }
    }

    private sealed class RegistrationProtocolStub(
        WorkerRegistrationResult? result,
        WorkerCredentialRotationResult? rotationResult = null) : IWorkerProtocolService
    {
        public bool WasCalled { get; private set; }

        public AuthenticatedWorker? Authenticated { get; init; }

        public (Guid WorkerId, Guid DeploymentId)? LastAuthentication { get; private set; }

        public Task<AuthenticatedWorker> AuthenticateAsync(
            Guid workerId,
            Guid deploymentId,
            string workerCredential,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            LastAuthentication = (workerId, deploymentId);
            return Authenticated is null
                ? throw new WorkerProtocolAuthenticationException()
                : Task.FromResult(Authenticated);
        }

        public Task<bool> HasDeliverableCommandAsync(
            Guid workerId,
            Guid deploymentId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkerRegistrationResult> RegisterAsync(
            Guid workerId,
            WorkerRegistrationRequest request,
            string bootstrapCredential,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(result ?? throw new InvalidOperationException("Unexpected protocol invocation."));
        }

        public Task<WorkerCredentialRotationResult> RotateCredentialAsync(
            Guid workerId,
            WorkerCredentialRotationRequest request,
            string workerCredential,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(
                rotationResult ?? throw new InvalidOperationException("Unexpected protocol invocation."));
        }

        public Task HeartbeatAsync(
            Guid workerId,
            WorkerHeartbeatRequest request,
            string workerCredential,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkerCommandClaimResponse?> ClaimCommandAsync(
            Guid workerId,
            WorkerCommandClaimRequest request,
            string workerCredential,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task ReportReceivedAsync(
            Guid workerId,
            Guid commandId,
            WorkerCommandStageRequest request,
            string workerCredential,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task ReportRunningAsync(
            Guid workerId,
            Guid commandId,
            WorkerCommandProcessRequest request,
            string workerCredential,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task ReportSucceededAsync(
            Guid workerId,
            Guid commandId,
            WorkerCommandProcessRequest request,
            string workerCredential,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task ReportFailedAsync(
            Guid workerId,
            Guid commandId,
            WorkerCommandFailureRequest request,
            string workerCredential,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}

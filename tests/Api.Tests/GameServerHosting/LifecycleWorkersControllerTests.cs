using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using X39.UnitedTacticalForces.Api.Controllers;
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

    private sealed class RegistrationProtocolStub(
        WorkerRegistrationResult? result,
        WorkerCredentialRotationResult? rotationResult = null) : IWorkerProtocolService
    {
        public bool WasCalled { get; private set; }

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

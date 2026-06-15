using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;
using X39.UnitedTacticalForces.Api.Services.WorkerProtocol;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.Api.Controllers;

/// <summary>
/// Private, credential-bound control-plane protocol for dedicated external workers.
/// It is intentionally excluded from the browser-facing OpenAPI document.
/// </summary>
[ApiController]
[AllowAnonymous]
[EnableRateLimiting(WorkerProtocolRateLimitPolicy.Name)]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("lifecycle-workers/{workerId:guid}")]
public sealed class LifecycleWorkersController(IWorkerProtocolService workerProtocolService) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType<WorkerRegistrationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterAsync(
        [FromRoute] Guid workerId,
        [FromBody] WorkerRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryReadBearerCredential(out var bootstrapCredential))
            return Unauthorized();
        try
        {
            var result = await workerProtocolService
                .RegisterAsync(workerId, request, bootstrapCredential, cancellationToken)
                .ConfigureAwait(false);
            WriteCredentialHeader(result.WorkerCredential);
            return Ok(result.Response);
        }
        catch (Exception ex) when (TryMapException(ex, out var result))
        {
            return result;
        }
    }

    [HttpPost("rotate-credential")]
    [ProducesResponseType<WorkerCredentialRotationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RotateCredentialAsync(
        [FromRoute] Guid workerId,
        [FromBody] WorkerCredentialRotationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryReadBearerCredential(out var workerCredential))
            return Unauthorized();
        try
        {
            var result = await workerProtocolService
                .RotateCredentialAsync(workerId, request, workerCredential, cancellationToken)
                .ConfigureAwait(false);
            WriteCredentialHeader(result.WorkerCredential);
            return Ok(result.Response);
        }
        catch (Exception ex) when (TryMapException(ex, out var result))
        {
            return result;
        }
    }

    [HttpPost("heartbeat")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> HeartbeatAsync(
        [FromRoute] Guid workerId,
        [FromBody] WorkerHeartbeatRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryReadBearerCredential(out var workerCredential))
            return Unauthorized();
        try
        {
            await workerProtocolService
                .HeartbeatAsync(workerId, request, workerCredential, cancellationToken)
                .ConfigureAwait(false);
            return NoContent();
        }
        catch (Exception ex) when (TryMapException(ex, out var result))
        {
            return result;
        }
    }

    [HttpPost("commands/claim")]
    [ProducesResponseType<WorkerCommandClaimResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ClaimCommandAsync(
        [FromRoute] Guid workerId,
        [FromBody] WorkerCommandClaimRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryReadBearerCredential(out var workerCredential))
            return Unauthorized();
        try
        {
            var command = await workerProtocolService
                .ClaimCommandAsync(workerId, request, workerCredential, cancellationToken)
                .ConfigureAwait(false);
            return command is null ? NoContent() : Ok(command);
        }
        catch (Exception ex) when (TryMapException(ex, out var result))
        {
            return result;
        }
    }

    [HttpPost("commands/{commandId:guid}/received")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> ReportReceivedAsync(
        [FromRoute] Guid workerId,
        [FromRoute] Guid commandId,
        [FromBody] WorkerCommandStageRequest request,
        CancellationToken cancellationToken)
        => ReportAsync(
            workerId,
            credential => workerProtocolService.ReportReceivedAsync(
                workerId,
                commandId,
                request,
                credential,
                cancellationToken));

    [HttpPost("commands/{commandId:guid}/running")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> ReportRunningAsync(
        [FromRoute] Guid workerId,
        [FromRoute] Guid commandId,
        [FromBody] WorkerCommandProcessRequest request,
        CancellationToken cancellationToken)
        => ReportAsync(
            workerId,
            credential => workerProtocolService.ReportRunningAsync(
                workerId,
                commandId,
                request,
                credential,
                cancellationToken));

    [HttpPost("commands/{commandId:guid}/succeeded")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> ReportSucceededAsync(
        [FromRoute] Guid workerId,
        [FromRoute] Guid commandId,
        [FromBody] WorkerCommandProcessRequest request,
        CancellationToken cancellationToken)
        => ReportAsync(
            workerId,
            credential => workerProtocolService.ReportSucceededAsync(
                workerId,
                commandId,
                request,
                credential,
                cancellationToken));

    [HttpPost("commands/{commandId:guid}/failed")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> ReportFailedAsync(
        [FromRoute] Guid workerId,
        [FromRoute] Guid commandId,
        [FromBody] WorkerCommandFailureRequest request,
        CancellationToken cancellationToken)
        => ReportAsync(
            workerId,
            credential => workerProtocolService.ReportFailedAsync(
                workerId,
                commandId,
                request,
                credential,
                cancellationToken));

    private async Task<IActionResult> ReportAsync(
        Guid workerId,
        Func<string, Task> report)
    {
        _ = workerId;
        if (!TryReadBearerCredential(out var workerCredential))
            return Unauthorized();
        try
        {
            await report(workerCredential).ConfigureAwait(false);
            return NoContent();
        }
        catch (Exception ex) when (TryMapException(ex, out var result))
        {
            return result;
        }
    }

    private bool TryReadBearerCredential(out string credential)
    {
        credential = string.Empty;
        var authorization = Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        if (!authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;
        credential = authorization[prefix.Length..].Trim();
        return credential.Length is > 0 and <= 512;
    }

    private void WriteCredentialHeader(string credential)
    {
        Response.Headers.Append(WorkerProtocolHeaders.WorkerCredential, credential);
        Response.Headers.Append(HeaderNames.CacheControl, "no-store");
        Response.Headers.Append(HeaderNames.Pragma, "no-cache");
    }

    private static bool TryMapException(Exception exception, out IActionResult result)
    {
        result = exception switch
        {
            WorkerProtocolAuthenticationException => new UnauthorizedResult(),
            WorkerProtocolValidationException => new BadRequestObjectResult(new { error = exception.Message }),
            WorkerProtocolNotFoundException => new NotFoundObjectResult(new { error = exception.Message }),
            WorkerProtocolConflictException => new ConflictObjectResult(new { error = exception.Message }),
            _ => null!,
        };
        return result is not null;
    }
}

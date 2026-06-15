using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;
using X39.UnitedTacticalForces.Api.Services.WorkerProtocol;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.Api.Controllers;

/// <summary>
/// Private, command-bound persistent-data protocol for dedicated external workers.
/// </summary>
[ApiController]
[AllowAnonymous]
[EnableRateLimiting(WorkerProtocolRateLimitPolicy.Name)]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("lifecycle-workers/{workerId:guid}/commands/{commandId:guid}/storage")]
public sealed class LifecycleWorkerStorageController(
    IWorkerStorageTransferService storageTransferService) : ControllerBase
{
    [HttpGet("persistent-snapshot")]
    [Produces(WorkerStorageProtocol.ArchiveMediaType)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DownloadPersistentSnapshotAsync(
        [FromRoute] Guid workerId,
        [FromRoute] Guid commandId,
        CancellationToken cancellationToken)
    {
        SetNoStoreHeaders();
        if (!TryReadBearerCredential(out var workerCredential))
            return Unauthorized();
        try
        {
            var download = await storageTransferService
                .CreatePersistentSnapshotAsync(workerId, commandId, workerCredential, cancellationToken)
                .ConfigureAwait(false);
            Response.ContentLength = download.Length;
            return File(download.Content, WorkerStorageProtocol.ArchiveMediaType);
        }
        catch (Exception ex) when (TryMapException(ex, out var result))
        {
            return result;
        }
    }

    [HttpGet("preservation-plan")]
    [ProducesResponseType<WorkerPreservationPlanResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetPreservationPlanAsync(
        [FromRoute] Guid workerId,
        [FromRoute] Guid commandId,
        CancellationToken cancellationToken)
    {
        SetNoStoreHeaders();
        if (!TryReadBearerCredential(out var workerCredential))
            return Unauthorized();
        try
        {
            var plan = await storageTransferService
                .GetPreservationPlanAsync(workerId, commandId, workerCredential, cancellationToken)
                .ConfigureAwait(false);
            return Ok(plan);
        }
        catch (Exception ex) when (TryMapException(ex, out var result))
        {
            return result;
        }
    }

    [HttpPost("preservation")]
    [Consumes(WorkerStorageProtocol.ArchiveMediaType)]
    [RequestSizeLimit(WorkerStorageProtocol.MaximumArchiveBytes)]
    [ProducesResponseType<WorkerPreservationPublicationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> PublishPreservationAsync(
        [FromRoute] Guid workerId,
        [FromRoute] Guid commandId,
        CancellationToken cancellationToken)
    {
        SetNoStoreHeaders();
        if (!TryReadBearerCredential(out var workerCredential))
            return Unauthorized();
        if (!HasStorageArchiveContentType()
            || Request.ContentLength is > WorkerStorageProtocol.MaximumArchiveBytes)
        {
            return BadRequest(new { error = "A bounded worker storage archive is required." });
        }
        try
        {
            var result = await storageTransferService
                .PublishPreservationAsync(
                    workerId,
                    commandId,
                    workerCredential,
                    Request.Body,
                    cancellationToken)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (Exception ex) when (TryMapException(ex, out var result))
        {
            return result;
        }
    }

    private bool HasStorageArchiveContentType()
        => MediaTypeHeaderValue.TryParse(Request.ContentType, out var contentType)
           && string.Equals(
               contentType.MediaType.Value,
               WorkerStorageProtocol.ArchiveMediaType,
               StringComparison.OrdinalIgnoreCase);

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

    private void SetNoStoreHeaders()
    {
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

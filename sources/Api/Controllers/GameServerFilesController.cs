using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Hosting.Contracts;
using StorageFailureSanitizer = X39.UnitedTacticalForces.Api.Services.HostingRuntime.HostingFailureSanitizer;

namespace X39.UnitedTacticalForces.Api.Controllers;

[ApiController]
[Authorize]
[Route(Constants.Routes.GameServers + "/{gameServerId:guid}/files/{context}")]
public sealed class GameServerFilesController(
    DatabaseContext dbContext,
    IServerFileStorage fileStorage,
    IServerFileClassifier fileClassifier,
    IGameServerDefinitionCatalog gameDefinitions,
    IPreservationSelectionResolver preservationSelectionResolver,
    IHostingAuthorizationService authorizationService,
    IHostingAuditWriter auditWriter,
    ILogger<GameServerFilesController> logger) : ControllerBase
{
    private const long MaximumUploadBytes = 16 * 1024 * 1024;

    [HttpGet]
    [Authorize(Claims.ResourceBased.Server.View)]
    [ProducesResponseType<IReadOnlyCollection<HostingFileDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> BrowseAsync(
        [FromRoute] Guid gameServerId,
        [FromRoute] string context,
        [FromQuery] string? path,
        CancellationToken cancellationToken)
    {
        if (!TryParseContext(context, out var fileContext))
            return BadRequest(new { error = "Unknown file context." });
        try
        {
            var deploymentId = await ResolveDeploymentAsync(gameServerId, fileContext, cancellationToken)
                .ConfigureAwait(false);
            var entries = await fileStorage
                .BrowseAsync(gameServerId, deploymentId, fileContext, path ?? string.Empty, cancellationToken)
                .ConfigureAwait(false);
            var capability = await GetCapabilityAsync(gameServerId, cancellationToken).ConfigureAwait(false);
            var canViewSensitive = HasModifyPermission(gameServerId);
            var projected = entries
                .Where(q => canViewSensitive
                            || GameServerFileCapabilityPolicy.Resolve(
                                capability,
                                q.RelativePath,
                                q.IsDirectory)?.Sensitive != true)
                .Select(q => Project(q, capability))
                .ToArray();
            if (fileContext is EFileContext.Persistent
                && await dbContext.GameServerDeployments.AnyAsync(
                    q => q.GameServerFk == gameServerId && q.TimeStampResolved == null,
                    cancellationToken).ConfigureAwait(false))
            {
                var selections = await GetSelectionsAsync(gameServerId, cancellationToken).ConfigureAwait(false);
                projected = projected
                    .Select(q => IsProtectedByPreservation(q.Path, q.IsDirectory, selections)
                        ? q with { CanModify = false }
                        : q)
                    .ToArray();
            }
            return Ok(projected);
        }
        catch (Exception ex) when (TryMapStorageException(ex, out var result))
        {
            return result;
        }
    }

    [HttpGet("content")]
    [Authorize(Claims.ResourceBased.Server.View)]
    public async Task<IActionResult> DownloadAsync(
        [FromRoute] Guid gameServerId,
        [FromRoute] string context,
        [FromQuery] string path,
        CancellationToken cancellationToken)
    {
        if (!TryParseContext(context, out var fileContext))
            return BadRequest(new { error = "Unknown file context." });
        try
        {
            var deploymentId = await ResolveDeploymentAsync(gameServerId, fileContext, cancellationToken)
                .ConfigureAwait(false);
            var capability = await GetCapabilityAsync(gameServerId, cancellationToken).ConfigureAwait(false);
            if (GameServerFileCapabilityPolicy.Resolve(capability, path, false)?.Sensitive == true
                && !HasModifyPermission(gameServerId))
                return Forbid();
            var download = await fileStorage
                .OpenReadAsync(gameServerId, deploymentId, fileContext, path, cancellationToken)
                .ConfigureAwait(false);
            return File(download.Content, "application/octet-stream", download.FileName, enableRangeProcessing: true);
        }
        catch (Exception ex) when (TryMapStorageException(ex, out var result))
        {
            return result;
        }
    }

    [HttpPut]
    [RequestSizeLimit(MaximumUploadBytes)]
    [ProducesResponseType<HostingFileDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UploadAsync(
        [FromRoute] Guid gameServerId,
        [FromRoute] string context,
        [FromQuery] string path,
        CancellationToken cancellationToken)
    {
        if (!TryParseContext(context, out var fileContext))
            return BadRequest(new { error = "Unknown file context." });
        if (!HasModifyPermission(gameServerId))
            return Forbid();
        try
        {
            if (!await CanMutatePathAsync(gameServerId, fileContext, path, false, cancellationToken)
                    .ConfigureAwait(false))
                return Conflict(new { error = "The selected file classification is read-only." });
            var deploymentId = await ResolveDeploymentAsync(gameServerId, fileContext, cancellationToken)
                .ConfigureAwait(false);
            await AuditMutationIntentAsync(
                gameServerId,
                deploymentId,
                EHostingAuditEventKind.FileUpdated,
                "Server file upload requested.",
                path,
                cancellationToken).ConfigureAwait(false);
            var entry = await fileStorage
                .UploadAsync(gameServerId, deploymentId, fileContext, path, Request.Body, true, cancellationToken)
                .ConfigureAwait(false);
            return Ok(Project(entry, await GetCapabilityAsync(gameServerId, cancellationToken).ConfigureAwait(false)));
        }
        catch (Exception ex) when (TryMapStorageException(ex, out var result))
        {
            return result;
        }
    }

    [HttpPost]
    [ProducesResponseType<HostingFileDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateDirectoryAsync(
        [FromRoute] Guid gameServerId,
        [FromRoute] string context,
        [FromBody] DirectoryCreationPayload payload,
        CancellationToken cancellationToken)
    {
        if (!TryParseContext(context, out var fileContext))
            return BadRequest(new { error = "Unknown file context." });
        if (!HasModifyPermission(gameServerId))
            return Forbid();
        try
        {
            if (!await CanMutatePathAsync(gameServerId, fileContext, payload.Path, true, cancellationToken)
                    .ConfigureAwait(false))
                return Conflict(new { error = "The selected directory classification is read-only." });
            var deploymentId = await ResolveDeploymentAsync(gameServerId, fileContext, cancellationToken)
                .ConfigureAwait(false);
            await AuditMutationIntentAsync(
                gameServerId,
                deploymentId,
                EHostingAuditEventKind.FileCreated,
                "Server directory creation requested.",
                payload.Path,
                cancellationToken).ConfigureAwait(false);
            var entry = await fileStorage
                .CreateDirectoryAsync(gameServerId, deploymentId, fileContext, payload.Path, cancellationToken)
                .ConfigureAwait(false);
            return Created(
                string.Empty,
                Project(entry, await GetCapabilityAsync(gameServerId, cancellationToken).ConfigureAwait(false)));
        }
        catch (Exception ex) when (TryMapStorageException(ex, out var result))
        {
            return result;
        }
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAsync(
        [FromRoute] Guid gameServerId,
        [FromRoute] string context,
        [FromQuery] string path,
        [FromQuery] bool recursive,
        CancellationToken cancellationToken)
    {
        if (!TryParseContext(context, out var fileContext))
            return BadRequest(new { error = "Unknown file context." });
        if (!HasModifyPermission(gameServerId))
            return Forbid();
        try
        {
            var deploymentId = await ResolveDeploymentAsync(gameServerId, fileContext, cancellationToken)
                .ConfigureAwait(false);
            var existing = await ResolveExistingEntryAsync(
                    gameServerId,
                    deploymentId,
                    fileContext,
                    path,
                    cancellationToken)
                .ConfigureAwait(false);
            if (existing is null)
                return NotFound();
            if (!await CanMutatePathAsync(
                    gameServerId,
                    fileContext,
                    path,
                    existing.IsDirectory,
                    cancellationToken)
                    .ConfigureAwait(false))
                return Conflict(new { error = "The selected file classification is read-only." });
            await AuditMutationIntentAsync(
                gameServerId,
                deploymentId,
                EHostingAuditEventKind.FileDeleted,
                "Server path deletion requested.",
                path,
                cancellationToken).ConfigureAwait(false);
            await fileStorage
                .DeleteAsync(gameServerId, deploymentId, fileContext, path, recursive, cancellationToken)
                .ConfigureAwait(false);
            return NoContent();
        }
        catch (Exception ex) when (TryMapStorageException(ex, out var result))
        {
            return result;
        }
    }

    private async Task<bool> CanMutatePathAsync(
        Guid gameServerId,
        EFileContext context,
        string path,
        bool isDirectory,
        CancellationToken cancellationToken)
    {
        var normalized = SafeServerPath.Normalize(path, false);
        var classification = fileClassifier.Classify(normalized, isDirectory);
        if (!fileClassifier.IsWritable(context, classification))
            return false;
        var capability = await GetCapabilityAsync(gameServerId, cancellationToken).ConfigureAwait(false);
        if (!GameServerFileCapabilityPolicy.CanMutate(capability, normalized, isDirectory, context))
            return false;
        if (context is not EFileContext.Persistent
            || !await dbContext.GameServerDeployments.AnyAsync(
                q => q.GameServerFk == gameServerId && q.TimeStampResolved == null,
                cancellationToken).ConfigureAwait(false))
            return true;

        var selections = await GetSelectionsAsync(gameServerId, cancellationToken).ConfigureAwait(false);
        return !IsProtectedByPreservation(normalized, isDirectory, selections);
    }

    private bool HasModifyPermission(Guid gameServerId)
        => authorizationService.HasServerPermission(
            User,
            gameServerId,
            Claims.ResourceBased.Server.Modify);

    private Task<PreservationPathSelection[]> GetSelectionsAsync(
        Guid gameServerId,
        CancellationToken cancellationToken)
        => dbContext.PreservationSelections
            .AsNoTracking()
            .Where(q => q.GameServerFk == gameServerId)
            .Select(q => new PreservationPathSelection(q.PrimaryKey, q.Path, q.Scope, q.Mode))
            .ToArrayAsync(cancellationToken);

    private bool IsProtectedByPreservation(
        string path,
        bool isDirectory,
        IReadOnlyCollection<PreservationPathSelection> selections)
    {
        if (preservationSelectionResolver.Resolve(path, isDirectory, selections) is not null)
            return true;
        if (!isDirectory)
            return false;
        var normalized = SafeServerPath.Normalize(path, false).TrimEnd('/');
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return selections.Any(q =>
        {
            var selected = SafeServerPath.Normalize(q.RelativePath, q.Scope is not EPreservationScope.File);
            return selected.Length == 0
                   || q.Scope is not EPreservationScope.File && selected.Equals(normalized, comparison)
                   || selected.StartsWith(normalized + "/", comparison);
        });
    }

    private async Task<Guid?> ResolveDeploymentAsync(
        Guid gameServerId,
        EFileContext context,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.GameServers
                .AsNoTracking()
                .AnyAsync(q => q.PrimaryKey == gameServerId && !q.IsArchived, cancellationToken)
                .ConfigureAwait(false))
            throw new FileNotFoundException("The game server does not exist.");
        if (context is EFileContext.Persistent)
            return null;
        return await dbContext.GameServerDeployments
                   .AsNoTracking()
                   .Where(q => q.GameServerFk == gameServerId && q.TimeStampResolved == null)
                   .Select(q => (Guid?) q.PrimaryKey)
                   .SingleOrDefaultAsync(cancellationToken)
                   .ConfigureAwait(false)
               ?? throw new DirectoryNotFoundException("The server has no active live runtime.");
    }

    private async Task<ServerFileEntry?> ResolveExistingEntryAsync(
        Guid gameServerId,
        Guid? deploymentId,
        EFileContext context,
        string path,
        CancellationToken cancellationToken)
    {
        var normalized = SafeServerPath.Normalize(path, false);
        var separator = normalized.LastIndexOf('/');
        var parent = separator < 0 ? string.Empty : normalized[..separator];
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var entries = await fileStorage
            .BrowseAsync(gameServerId, deploymentId, context, parent, cancellationToken)
            .ConfigureAwait(false);
        return entries.SingleOrDefault(q => q.RelativePath.Equals(normalized, comparison));
    }

    private async Task AuditMutationIntentAsync(
        Guid serverId,
        Guid? deploymentId,
        EHostingAuditEventKind kind,
        string summary,
        string path,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var actorId))
            throw new UnauthorizedAccessException();
        auditWriter.Add(
            dbContext,
            kind,
            summary,
            actorId,
            serverId,
            deploymentId,
            details: $"Path={SafeServerPath.Normalize(path, false)}");
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<GameServerDefinition> GetCapabilityAsync(
        Guid gameServerId,
        CancellationToken cancellationToken)
    {
        var gameIdentifier = await dbContext.GameServers
            .AsNoTracking()
            .Where(q => q.PrimaryKey == gameServerId)
            .Select(q => q.GameIdentifier)
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);
        return gameDefinitions.GetDefinitions().Single(q => string.Equals(
            q.GameIdentifier,
            gameIdentifier,
            StringComparison.OrdinalIgnoreCase));
    }

    private static HostingFileDto Project(ServerFileEntry entry, GameServerDefinition capability)
    {
        var gameRule = GameServerFileCapabilityPolicy.Resolve(
            capability,
            entry.RelativePath,
            entry.IsDirectory);
        return new HostingFileDto
        {
            Path = entry.RelativePath,
            Name = entry.Name,
            IsDirectory = entry.IsDirectory,
            Size = entry.Size,
            ModifiedAt = Instant.FromDateTimeOffset(entry.ModifiedAt),
            Context = entry.Context,
            Classification = gameRule?.Classification ?? entry.Classification,
            CanModify = entry.IsWritable
                        && GameServerFileCapabilityPolicy.CanMutate(
                            capability,
                            entry.RelativePath,
                            entry.IsDirectory,
                            entry.Context),
        };
    }

    private static bool TryParseContext(string context, out EFileContext parsed)
        => Enum.TryParse(context, true, out parsed) && Enum.IsDefined(parsed);

    private bool TryMapStorageException(Exception exception, out IActionResult result)
    {
        if (exception is IOException)
        {
            logger.LogWarning(
                "A game-server storage request failed: {Failure}",
                StorageFailureSanitizer.Sanitize(exception.Message));
        }
        result = exception switch
        {
            FileNotFoundException or DirectoryNotFoundException => new NotFoundObjectResult(new
            {
                error = "The requested file or directory does not exist.",
            }),
            ArgumentException => new BadRequestObjectResult(new { error = exception.Message }),
            UnauthorizedAccessException => new ForbidResult(),
            IOException => new ConflictObjectResult(new
            {
                error = "The storage operation could not be completed.",
            }),
            LiveWorkerFileAccessException => new ObjectResult(new
            {
                error = "Live files are available only through the authenticated cloud worker.",
            })
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable,
            },
            _ => null!,
        };
        return result is not null;
    }

    public sealed record DirectoryCreationPayload
    {
        public string Path { get; init; } = string.Empty;
    }
}

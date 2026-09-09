using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.ExtensionMethods;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Core;

namespace X39.UnitedTacticalForces.Api.Controllers;

/// <summary>Previews and downloads game-owned mod-pack formats.</summary>
[ApiController]
[Authorize]
[Route("game-content/mod-packs/formats")]
public sealed class ModPackFormatsController(
    DatabaseContext dbContext,
    IEnumerable<IModPackFormatHandler> handlers,
    IEnumerable<IModSourceProvider> providers) : ControllerBase
{
    /// <summary>Parses a custom file into reviewed normalized catalog candidates.</summary>
    /// <param name="gameIdentifier">Owning game identifier.</param>
    /// <param name="formatIdentifier">Format identifier.</param>
    /// <param name="file">File to preview.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Resolved entries, catalog matches, and warnings.</returns>
    [HttpPost("{gameIdentifier}/{formatIdentifier}/preview")]
    [RequestSizeLimit(4 * 1024 * 1024)]
    public async Task<IActionResult> PreviewAsync(
        string gameIdentifier,
        string formatIdentifier,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var handler = FindHandler(gameIdentifier, formatIdentifier);
        if (handler is null) return NotFound();
        if (file.Length <= 0 || file.Length > handler.Definition.MaximumSizeBytes)
            return BadRequest("The imported mod-pack file is empty or too large.");
        ModPackFormatImportResult import;
        try
        {
            await using var stream = file.OpenReadStream();
            import = await handler.ImportAsync(stream, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is InvalidDataException or FormatException)
        {
            return BadRequest("The imported mod-pack format is invalid.");
        }
        var resolved = new List<object>();
        foreach (var entry in import.Entries)
        {
            var provider = providers.FirstOrDefault(q => q.CanResolve(entry.SourceInput));
            if (provider is null) return BadRequest($"No provider recognizes '{entry.SourceInput}'.");
            try
            {
                var source = await provider.ResolveAsync(entry.SourceInput, null, cancellationToken).ConfigureAwait(false);
                var existingId = await dbContext.Mods.Where(q => q.GameIdentifier == gameIdentifier
                        && q.ProviderIdentifier == source.ProviderIdentifier
                        && q.CanonicalSourceIdentifier == source.CanonicalSourceIdentifier)
                    .Select(q => (Guid?)q.PrimaryKey).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
                resolved.Add(new { entry.Title, entry.SourceInput, Source = source, ExistingModId = existingId });
            }
            catch (ModSourceUnavailableException exception)
            {
                return Problem(exception.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (ArgumentException exception) { return BadRequest(exception.Message); }
        }
        return Ok(new { import.Title, Entries = resolved, import.Warnings });
    }

    /// <summary>Lists formats available for a revision.</summary>
    /// <param name="revisionId">Revision identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Format metadata.</returns>
    [HttpGet("/game-content/mod-packs/revisions/{revisionId:long}/formats")]
    public async Task<IActionResult> GetFormatsAsync(long revisionId, CancellationToken cancellationToken)
    {
        var game = await dbContext.ModPackRevisions.Where(q => q.PrimaryKey == revisionId)
            .Select(q => q.Definition!.GameIdentifier).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return game is null ? NotFound() : Ok(handlers.Where(q => q.GameIdentifier == game).Select(q => q.Definition));
    }

    /// <summary>Exports an immutable revision through a game-owned format.</summary>
    /// <param name="revisionId">Revision identifier.</param>
    /// <param name="formatIdentifier">Format identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated format file.</returns>
    [HttpGet("/game-content/mod-packs/revisions/{revisionId:long}/formats/{formatIdentifier}")]
    public async Task<IActionResult> DownloadAsync(
        long revisionId,
        string formatIdentifier,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId)) return Unauthorized();
        return await ExportRevisionAsync(revisionId, formatIdentifier, null, userId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Lists formats for a visible event's selected revision.</summary>
    /// <param name="eventId">Event identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Format metadata.</returns>
    [AllowAnonymous]
    [HttpGet("/events/{eventId:guid}/mod-pack-formats")]
    public async Task<IActionResult> GetEventFormatsAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var selection = await GetVisibleEventSelectionAsync(eventId, cancellationToken);
        if (selection.Result is not null) return selection.Result;
        return Ok(handlers.Where(q => q.GameIdentifier == selection.GameIdentifier).Select(q => q.Definition));
    }

    /// <summary>Downloads a visible event's selected mod pack through a game-owned format.</summary>
    /// <param name="eventId">Event identifier.</param>
    /// <param name="formatIdentifier">Format identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated format file.</returns>
    [AllowAnonymous]
    [HttpGet("/events/{eventId:guid}/mod-pack-formats/{formatIdentifier}")]
    public async Task<IActionResult> DownloadEventFormatAsync(
        Guid eventId,
        string formatIdentifier,
        CancellationToken cancellationToken)
    {
        var selection = await GetVisibleEventSelectionAsync(eventId, cancellationToken);
        if (selection.Result is not null) return selection.Result;
        var userId = User.TryGetUserId(out var authenticatedUserId) ? authenticatedUserId : (Guid?)null;
        return await ExportRevisionAsync(
                selection.RevisionId!.Value,
                formatIdentifier,
                eventId,
                userId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<IActionResult> ExportRevisionAsync(
        long revisionId,
        string formatIdentifier,
        Guid? eventId,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var revision = await dbContext.ModPackRevisions.AsNoTracking().Where(q => q.PrimaryKey == revisionId)
            .Select(q => new { q.DefinitionFk, q.Definition!.GameIdentifier, q.Definition.Title })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (revision is null) return NotFound();
        var handler = FindHandler(revision.GameIdentifier, formatIdentifier);
        if (handler is null) return NotFound();
        var entries = new List<ModPackFormatExportEntry>();
        await FlattenAsync(revisionId, new HashSet<long>(), new HashSet<Guid>(), entries, cancellationToken);
        var export = await handler.ExportAsync(revision.Title, entries, cancellationToken).ConfigureAwait(false);
        if (export.Warnings.Count > 0) Response.Headers.Append("X-Mod-Pack-Warnings", string.Join(" | ", export.Warnings));
        dbContext.ModPackFormatDeliveries.Add(new ModPackFormatDelivery
        {
            PrimaryKey = Guid.NewGuid(),
            TimeStampCreated = NodaSystemClock.Instance.GetCurrentInstant(),
            ModPackRevisionFk = revisionId,
            FormatIdentifier = handler.Definition.Identifier,
            EventFk = eventId,
            UserFk = userId,
        });
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return File(export.Content, handler.Definition.MediaType,
            $"{SanitizeFileName(revision.Title)}.{handler.Definition.FileExtension}");
    }

    private async Task FlattenAsync(
        long revisionId,
        ISet<long> active,
        ISet<Guid> resolvedMods,
        ICollection<ModPackFormatExportEntry> entries,
        CancellationToken cancellationToken)
    {
        if (!active.Add(revisionId)) throw new InvalidOperationException("A persisted mod-pack cycle was detected.");
        var direct = await dbContext.ModPackRevisionMods.AsNoTracking().Where(q => q.ModPackRevisionFk == revisionId)
            .OrderBy(q => q.Order).Select(q => new
            {
                q.ModFk, q.Mod!.Title, q.Mod.ProviderIdentifier, q.Mod.CanonicalSourceIdentifier, q.Mod.UsageRole,
            }).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        foreach (var mod in direct)
            if (resolvedMods.Add(mod.ModFk))
                entries.Add(new(mod.Title, mod.ProviderIdentifier, mod.CanonicalSourceIdentifier, mod.UsageRole));
        var dependencies = await dbContext.ModPackRevisionDependencies.AsNoTracking()
            .Where(q => q.ParentRevisionFk == revisionId).OrderBy(q => q.Order).ToArrayAsync(cancellationToken);
        foreach (var dependency in dependencies)
        {
            var child = dependency.PinnedChildRevisionFk ?? await dbContext.ModPackRevisions
                .Where(q => q.DefinitionFk == dependency.ChildDefinitionFk && q.IsActive)
                .OrderByDescending(q => q.TimeStampCreated).Select(q => q.PrimaryKey)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (child == 0) throw new InvalidOperationException("A nested mod pack has no active revision.");
            await FlattenAsync(child, active, resolvedMods, entries, cancellationToken);
        }
        active.Remove(revisionId);
    }

    private async Task<(IActionResult? Result, long? RevisionId, string GameIdentifier)> GetVisibleEventSelectionAsync(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Events.AsNoTracking().Where(q => q.PrimaryKey == eventId)
            .Select(q => new { q.IsVisible, q.OwnerFk, q.HostedByFk, q.ModPackRevisionFk,
                GameIdentifier = q.ModPackRevision == null ? null : q.ModPackRevision.Definition!.GameIdentifier })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (item is null) return (NotFound(), null, string.Empty);
        var allowed = item.IsVisible || User.IsGlobalAdministrator()
            || User.TryGetUserId(out var userId) && (userId == item.OwnerFk || userId == item.HostedByFk);
        if (!allowed) return (Forbid(), null, string.Empty);
        if (item.ModPackRevisionFk is null || item.GameIdentifier is null) return (NotFound(), null, string.Empty);
        return (null, item.ModPackRevisionFk, item.GameIdentifier);
    }

    private IModPackFormatHandler? FindHandler(string gameIdentifier, string formatIdentifier)
        => handlers.FirstOrDefault(q => q.GameIdentifier.Equals(gameIdentifier, StringComparison.OrdinalIgnoreCase)
                                        && q.Definition.Identifier.Equals(formatIdentifier, StringComparison.Ordinal));

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Trim().Select(q => invalid.Contains(q) ? '_' : q).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "mod-pack" : sanitized;
    }
}

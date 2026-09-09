using System.Text.RegularExpressions;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.GameServerDefinition;

/// <summary>Filesystem-backed immutable reusable mod artifact storage.</summary>
[Singleton<FileSystemModArtifactStorage, IModArtifactStorage>]
public sealed partial class FileSystemModArtifactStorage(IWebHostEnvironment environment) : IModArtifactStorage
{
    private readonly string _root = Path.Combine(environment.ContentRootPath, "data", "mod-artifacts");

    /// <inheritdoc />
    public async Task StoreAsync(string artifactIdentifier, Stream content, CancellationToken cancellationToken = default)
    {
        var path = Resolve(artifactIdentifier);
        Directory.CreateDirectory(_root);
        var temporaryPath = Path.Combine(_root, $".{artifactIdentifier}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var output = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             81920,
                             true))
            {
                await content.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            File.Move(temporaryPath, path, false);
        }
        catch
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            throw;
        }
    }

    /// <inheritdoc />
    public Task<ModArtifactDownload> OpenReadAsync(
        string artifactIdentifier,
        CancellationToken cancellationToken = default)
    {
        var path = Resolve(artifactIdentifier);
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        return Task.FromResult(new ModArtifactDownload(stream, stream.Length));
    }

    /// <inheritdoc />
    public Task DeleteAsync(string artifactIdentifier, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = Resolve(artifactIdentifier);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string Resolve(string artifactIdentifier)
    {
        if (!ArtifactIdentifierRegex().IsMatch(artifactIdentifier))
            throw new ArgumentException("The artifact identifier is invalid.", nameof(artifactIdentifier));
        return Path.Combine(_root, $"{artifactIdentifier}.zip");
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex ArtifactIdentifierRegex();
}

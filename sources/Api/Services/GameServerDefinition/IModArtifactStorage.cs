namespace X39.UnitedTacticalForces.Api.Services.GameServerDefinition;

/// <summary>Stores immutable reusable mod-version artifacts outside server-scoped storage.</summary>
public interface IModArtifactStorage
{
    /// <summary>Stores a new immutable artifact.</summary>
    /// <param name="artifactIdentifier">Validated artifact identifier.</param>
    /// <param name="content">Artifact bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing completion.</returns>
    Task StoreAsync(string artifactIdentifier, Stream content, CancellationToken cancellationToken = default);
    /// <summary>Opens an immutable artifact for reading.</summary>
    /// <param name="artifactIdentifier">Artifact identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Readable artifact.</returns>
    Task<ModArtifactDownload> OpenReadAsync(string artifactIdentifier, CancellationToken cancellationToken = default);

    /// <summary>Deletes an artifact when catalog mutation compensation is required.</summary>
    /// <param name="artifactIdentifier">Artifact identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing completion.</returns>
    Task DeleteAsync(string artifactIdentifier, CancellationToken cancellationToken = default);
}

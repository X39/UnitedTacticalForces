namespace X39.UnitedTacticalForces.Api.Services.GameServerDefinition;

/// <summary>Readable reusable mod artifact.</summary>
/// <param name="Content">Artifact content.</param>
/// <param name="Length">Artifact size.</param>
public sealed record ModArtifactDownload(Stream Content, long Length) : IAsyncDisposable
{
    /// <inheritdoc />
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

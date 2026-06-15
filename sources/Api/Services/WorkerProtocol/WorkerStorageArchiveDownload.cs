namespace X39.UnitedTacticalForces.Api.Services.WorkerProtocol;

public sealed class WorkerStorageArchiveDownload(Stream content) : IAsyncDisposable
{
    public Stream Content { get; } = content;

    public long Length => Content.Length;

    public ValueTask DisposeAsync() => Content.DisposeAsync();
}
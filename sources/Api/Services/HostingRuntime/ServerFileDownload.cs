namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

public sealed class ServerFileDownload : IAsyncDisposable, IDisposable
{
    public ServerFileDownload(Stream content, string fileName, long length)
    {
        Content  = content;
        FileName = fileName;
        Length   = length;
    }

    public Stream Content { get; }

    public string FileName { get; }

    public long Length { get; }

    public void Dispose() => Content.Dispose();

    public ValueTask DisposeAsync() => Content.DisposeAsync();
}
namespace CloudWorker.Tests;

/// <summary>
/// A stream decorator whose writes can be stalled until cancelled, so tests can make a WebSocket send hang.
/// </summary>
/// <param name="inner">The wrapped stream.</param>
internal sealed class GatedStream(Stream inner) : Stream
{
    private volatile bool _blocked;

    /// <summary>Gets or sets whether writes stall until their cancellation token fires.</summary>
    /// <value><see langword="true"/> while writes are stalled.</value>
    public bool BlockWrites
    {
        get => _blocked;
        set => _blocked = value;
    }

    /// <inheritdoc />
    public override bool CanRead => inner.CanRead;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => inner.CanWrite;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override void Flush() => inner.Flush();

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => inner.ReadAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);

    /// <inheritdoc />
    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        if (_blocked)
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        await inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            inner.Dispose();
        base.Dispose(disposing);
    }
}

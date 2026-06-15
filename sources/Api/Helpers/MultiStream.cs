using System.Collections.Immutable;
using System.ComponentModel;

namespace X39.UnitedTacticalForces.Api.Helpers;

public class MultiStream : Stream
{
    private readonly IReadOnlyCollection<Stream> _streams;

    public MultiStream(IEnumerable<Stream> streams)
    {
        _streams = streams.ToImmutableArray();
    }

    /// <inheritdoc />
    public override void Flush()
    {
        foreach (var stream in _streams)
        {
            stream.Flush();
        }
    }

    /// <inheritdoc />
    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        foreach (var stream in _streams)
        {
            await stream.FlushAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        long result = default;
        foreach (var stream in _streams)
        {
            result = stream.Seek(offset, origin);
        }

        return result;
    }

    /// <inheritdoc />
    public override void SetLength(long value)
    {
        foreach (var stream in _streams)
        {
            stream.SetLength(value);
        }
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        foreach (var stream in _streams)
        {
            stream.Write(buffer, offset, count);
        }
    }

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        foreach (var stream in _streams)
        {
            stream.Write(buffer);
        }
    }

    /// <inheritdoc />
    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        foreach (var stream in _streams)
        {
            await stream.WriteAsync(buffer, offset, count, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = new CancellationToken())
    {
        foreach (var stream in _streams)
        {
            await stream.WriteAsync(buffer, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public override void WriteByte(byte value)
    {
        foreach (var stream in _streams)
        {
            stream.WriteByte(value);
        }
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public override bool CanRead => false;

    public override bool CanSeek => _streams.All(s => s.CanSeek);
    public override bool CanWrite => _streams.All(s => s.CanWrite);
    public override long Length => _streams.Max(s => s.Length);

    public override long Position
    {
        get => _streams.Min(s => s.Position);
        set => Seek(value, SeekOrigin.Begin);
    }

    protected override void Dispose(bool disposing)
    {
        foreach (var stream in _streams)
        {
            stream.Dispose();
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        foreach (var stream in _streams)
        {
            await stream.DisposeAsync().ConfigureAwait(false);
        }

        await base.DisposeAsync().ConfigureAwait(false);
    }
}
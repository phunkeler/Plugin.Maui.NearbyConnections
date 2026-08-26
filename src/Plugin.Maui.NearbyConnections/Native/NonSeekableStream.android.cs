namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// A forward-only wrapper over a GMS pipe stream, used in both directions (story S8).
/// </summary>
/// <remarks>
/// <para>
/// The GMS stream is backed by a pipe file descriptor. Android's own
/// <c>InputStreamInvoker</c> reports <see cref="Stream.CanSeek"/> as <see langword="true"/> and
/// then throws <c>java.io.IOException: Illegal seek</c> the moment
/// <see cref="Stream.Position"/> is read — and <see cref="Stream.CopyToAsync(Stream)"/> reads it,
/// through <c>GetCopyBufferSize</c>. So the most ordinary thing a consumer does with a stream
/// crashed, on Android only.
/// </para>
/// <para>
/// This wrapper tells the truth about the pipe: not seekable, no length, no position. It is the
/// Android counterpart to the iOS <c>NsInputStreamAdapter</c>, so a consumer sees the same stream
/// shape on both platforms.
/// </para>
/// </remarks>
/// <param name="inner">The platform stream to wrap.</param>
sealed class NonSeekableStream(Stream inner) : Stream
{
    public override bool CanRead => inner.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => inner.CanWrite;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => inner.Flush();

    public override int Read(byte[] buffer, int offset, int count)
        => inner.Read(buffer, offset, count);

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => inner.ReadAsync(buffer, offset, count, cancellationToken);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => inner.ReadAsync(buffer, cancellationToken);

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
        => inner.Write(buffer, offset, count);

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => inner.WriteAsync(buffer, offset, count, cancellationToken);

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        => inner.WriteAsync(buffer, cancellationToken);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            inner.Dispose();
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        await inner.DisposeAsync().ConfigureAwait(false);
        await base.DisposeAsync().ConfigureAwait(false);
    }
}

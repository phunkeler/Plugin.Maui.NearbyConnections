namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// A writable <see cref="Stream"/> over the <see cref="NSOutputStream"/> MultipeerConnectivity
/// hands out for an outbound named stream. Polls <see cref="NSOutputStream.HasSpaceAvailable"/>
/// rather than scheduling a run loop — the minimal viable shape stage M6 ships with.
/// </summary>
sealed class NsOutputStreamAdapter : Stream
{
    static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(10);

    readonly NSOutputStream _inner;

    internal NsOutputStreamAdapter(NSOutputStream inner)
    {
        _inner = inner;
        _inner.Open();
    }

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
        // NSOutputStream writes through; there is no buffer of this adapter's own to flush.
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
    {
        while (count > 0)
        {
            if (_inner.Status is NSStreamStatus.Closed or NSStreamStatus.Error)
            {
                throw new IOException("The stream to the remote device closed.");
            }

            if (!_inner.HasSpaceAvailable())
            {
                Thread.Sleep(PollInterval);
                continue;
            }

            var slice = new byte[count];
            Array.Copy(buffer, offset, slice, 0, count);
            var written = (int)_inner.Write(slice, (nuint)count);

            if (written < 0)
            {
                throw new IOException("The stream to the remote device rejected the write.");
            }

            offset += written;
            count -= written;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Close();
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }
}

/// <summary>
/// A readable <see cref="Stream"/> over the <see cref="NSInputStream"/> MultipeerConnectivity
/// delivers for an inbound named stream. Same polling shape as the writer.
/// </summary>
sealed class NsInputStreamAdapter : Stream
{
    static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(10);

    readonly NSInputStream _inner;

    internal NsInputStreamAdapter(NSInputStream inner)
    {
        _inner = inner;
        _inner.Open();
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
        // Read-only.
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        while (true)
        {
            if (_inner.HasBytesAvailable())
            {
                var slice = new byte[count];
                var read = (int)_inner.Read(slice, (nuint)count);

                if (read < 0)
                {
                    throw new IOException("The stream from the remote device failed.");
                }

                if (read == 0)
                {
                    return 0;
                }

                Array.Copy(slice, 0, buffer, offset, read);
                return read;
            }

            if (_inner.Status is NSStreamStatus.AtEnd or NSStreamStatus.Closed or NSStreamStatus.Error)
            {
                return 0;
            }

            Thread.Sleep(PollInterval);
        }
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Close();
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }
}

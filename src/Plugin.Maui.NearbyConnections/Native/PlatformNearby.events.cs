namespace Plugin.Maui.NearbyConnections;

sealed partial class PlatformNearby
{
    internal void WriteDeviceFound(NearbyDevice device)
        => WriteDeviceEvent(device, found: true, nameof(WriteDeviceFound));

    internal void WriteDeviceLost(NearbyDevice device)
        => WriteDeviceEvent(device, found: false, nameof(WriteDeviceLost));

    void WriteDeviceEvent(NearbyDevice device, bool found, string writer)
    {
        try
        {
            var channel = _discoverChannel;

            if (!channel.Writer.TryWrite(new NearbyDeviceEvent(device, found)))
            {
                LogWriteChannelCompleted(writer, device.Id);
            }
        }
        catch (Exception ex)
        {
            LogWriteError(writer, device.Id, ex);
        }
    }

    internal void WriteConnectionRequest(NearbyConnectionRequest request)
    {
        try
        {
            var channel = _advertiseChannel;
            var written = channel.Writer.TryWrite(request);

            if (!written)
            {
                LogWriteChannelCompleted(nameof(WriteConnectionRequest), request.RemoteDevice.Id);
                _ = RejectUnroutableRequestAsync(request);
            }
        }
        catch (Exception ex)
        {
            LogWriteError(nameof(WriteConnectionRequest), request.RemoteDevice.Id, ex);
        }
    }

    async Task RejectUnroutableRequestAsync(NearbyConnectionRequest request)
    {
        try
        {
            await request.RejectAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogWriteError(nameof(WriteConnectionRequest), request.RemoteDevice.Id, ex);
        }
    }

    /// <summary>
    /// Registers a pending handshake for <paramref name="peerId"/> and returns the source the
    /// platform's terminal callback will resolve or fault.
    /// </summary>
    /// <remarks>
    /// The completion side of the handshake lifecycle has <see cref="ResolveConnectionTcs"/> and
    /// <see cref="FaultConnectionTcs"/>; this is the registration side, so all three sit together.
    /// <para>
    /// <paramref name="cancellationToken"/> is the token <c>DisposeAsync</c> attributes its
    /// cancellation to. Pass the caller's token whenever one exists — a handshake registered with
    /// <see cref="CancellationToken.None"/> produces an <see cref="OperationCanceledException"/>
    /// the awaiter cannot correlate to its own operation. Android registers before any caller
    /// token exists (the request arrives on a platform callback), so it re-registers via
    /// <see cref="AttachConnectionTcsToken"/> once <c>AcceptAsync</c> supplies one.
    /// </para>
    /// </remarks>
    internal TaskCompletionSource<NearbyConnection> RegisterConnectionTcs(
        string peerId,
        CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<NearbyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);

        _connectionTcs[peerId] = (tcs, cancellationToken);

        return tcs;
    }

    /// <summary>
    /// Re-points an already-registered handshake at <paramref name="cancellationToken"/>, for the
    /// platform that learns the caller's token only after the request has been surfaced. Leaves a
    /// handshake that has already completed alone.
    /// </summary>
    internal void AttachConnectionTcsToken(string peerId, CancellationToken cancellationToken)
    {
        if (_connectionTcs.TryGetValue(peerId, out var entry))
        {
            _connectionTcs.TryUpdate(peerId, (entry.Tcs, cancellationToken), entry);
        }
    }

    internal void ResolveConnectionTcs(string peerId, NearbyConnection connection)
    {
        try
        {
            if (_connectionTcs.TryRemove(peerId, out var entry))
            {
                _activeConnections[peerId] = connection;
                entry.Tcs.TrySetResult(connection);
            }
        }
        catch (Exception ex)
        {
            LogWriteError(nameof(ResolveConnectionTcs), peerId, ex);
        }
    }

    internal void FaultConnectionTcs(string peerId, Exception ex)
    {
        try
        {
            if (_connectionTcs.TryRemove(peerId, out var entry))
            {
                entry.Tcs.TrySetException(ex);
            }
        }
        catch (Exception innerEx)
        {
            LogWriteError(nameof(FaultConnectionTcs), peerId, innerEx);
        }
    }

    /// <summary>
    /// Releases the platform's bookkeeping for a connection that has ended: removes it from
    /// <c>_activeConnections</c>, ends its receive stream, and clears the unobserved-payload
    /// warning latch. Platform-specific cleanup hangs off <see cref="PlatformReleaseConnection"/>.
    /// </summary>
    /// <remarks>
    /// Safe to call for a peer with no active connection, and safe to call twice: the
    /// <c>TryRemove</c> guard means <c>CompleteReceive</c> runs at most once per connection.
    /// </remarks>
    internal void ReleaseConnection(string peerId)
    {
        if (_activeConnections.TryRemove(peerId, out var connection))
        {
            connection.CompleteReceive();
        }

        _unobservedWarned.TryRemove(peerId, out _);

        PlatformReleaseConnection(peerId);
    }

    /// <summary>
    /// Platform-specific half of <see cref="ReleaseConnection"/>. Implemented on iOS to drop the
    /// peer's KVO progress observers; on other platforms the call compiles away.
    /// </summary>
    partial void PlatformReleaseConnection(string peerId);

    internal void WritePayload(string peerId, NearbyPayload payload)
    {
        try
        {
            if (!_activeConnections.TryGetValue(peerId, out var connection))
            {
                LogWritePayloadNoConnection(peerId);
                return;
            }

            if (!connection.IsBeingConsumed && _unobservedWarned.TryAdd(peerId, 0))
            {
                LogPayloadArrivedUnobserved(peerId);
            }

            connection.TryWritePayload(payload);
        }
        catch (Exception ex)
        {
            LogWriteError(nameof(WritePayload), peerId, ex);
        }
    }

    /// <summary>
    /// Resolves a non-colliding destination path for an inbound file, appending " (n)" before the
    /// extension when the name is already taken.
    /// </summary>
    /// <remarks>
    /// Both platforms previously combined <see cref="NearbyOptions.ReceivedFilesDirectory"/>
    /// with the sender-supplied name and overwrote unconditionally, so two peers sending
    /// <c>photo.jpg</c> silently clobbered one another and the app saw only the last one. This is
    /// best-effort, not atomic: a concurrent transfer could claim the same name between the check
    /// and the write. That race is far narrower than the unconditional overwrite it replaces, and
    /// closing it fully needs file-creation-based reservation, which is a larger change than this
    /// fix pass warrants.
    /// </remarks>
    internal static string ResolveUniqueDestinationPath(string directory, string fileName)
    {
        var candidate = Path.Combine(directory, fileName);

        if (!File.Exists(candidate))
        {
            return candidate;
        }

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);

        for (var i = 1; ; i++)
        {
            candidate = Path.Combine(directory, $"{stem} ({i}){extension}");

            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }
}
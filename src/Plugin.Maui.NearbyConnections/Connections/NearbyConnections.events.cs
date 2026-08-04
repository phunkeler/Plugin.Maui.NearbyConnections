namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyConnectionsImplementation
{
    internal void WriteDeviceFound(NearbyDevice device)
    {
        try
        {
            var channel = _discoverChannel;
            var written = channel.Writer.TryWrite(new NearbyDeviceEvent(device, NearbyDeviceEventType.Found));

            if (!written)
            {
                LogWriteDeviceFoundChannelCompleted(device.Id);
            }
        }
        catch (Exception ex)
        {
            LogWriteDeviceFoundError(device.Id, ex);
        }
    }

    internal void WriteDeviceLost(NearbyDevice device)
    {
        try
        {
            var channel = _discoverChannel;
            var written = channel.Writer.TryWrite(new NearbyDeviceEvent(device, NearbyDeviceEventType.Lost));

            if (!written)
            {
                LogWriteDeviceLostChannelCompleted(device.Id);
            }
        }
        catch (Exception ex)
        {
            LogWriteDeviceLostError(device.Id, ex);
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
                LogWriteConnectionRequestChannelCompleted(request.RemoteDevice.Id);
                _ = request.RejectAsync();
            }
        }
        catch (Exception ex)
        {
            LogWriteConnectionRequestError(request.RemoteDevice.Id, ex);
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
            LogResolveConnectionTcsError(peerId, ex);
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
            LogFaultConnectionTcsError(peerId, innerEx);
        }
    }

    internal void WritePayload(string peerId, NearbyPayload payload)
    {
        try
        {
            if (!_activeConnections.TryGetValue(peerId, out var connection))
            {
                LogWritePayloadNoConnection(peerId);
                return;
            }

            // Nothing ever called ReceiveAsync, so this payload — and every one after it — goes
            // into an unbounded channel with no reader and is never seen. The write still happens
            // (a consumer that starts late drains the backlog), but the condition is a bug in the
            // consuming app and is otherwise completely silent. Warn once per connection.
            if (!connection.IsBeingConsumed && _unobservedWarned.TryAdd(peerId, 0)) // value unused
            {
                LogPayloadArrivedUnobserved(peerId);
            }

            connection.TryWritePayload(payload);
        }
        catch (Exception ex)
        {
            LogWritePayloadError(peerId, ex);
        }
    }

    /// <summary>
    /// Resolves a non-colliding destination path for an inbound file, appending " (n)" before the
    /// extension when the name is already taken.
    /// </summary>
    /// <remarks>
    /// Both platforms previously combined <see cref="NearbyConnectionsOptions.ReceivedFilesDirectory"/>
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

        for (var i = 1; i < int.MaxValue; i++)
        {
            candidate = Path.Combine(directory, $"{stem} ({i}){extension}");

            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        // Unreachable in practice; keeps the compiler happy about definite return.
        return Path.Combine(directory, $"{stem} ({Guid.NewGuid():N}){extension}");
    }
}

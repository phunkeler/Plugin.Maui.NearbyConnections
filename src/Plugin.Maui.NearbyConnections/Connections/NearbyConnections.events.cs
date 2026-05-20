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
                FaultConnectionTcs(
                    request.RemoteDevice.Id,
                    new ObjectDisposedException(nameof(NearbyConnectionsImplementation), "Advertise channel is already completed."));
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
            if (_activeConnections.TryGetValue(peerId, out var connection))
            {
                connection.TryWritePayload(payload);
            }
        }
        catch (Exception ex)
        {
            LogWritePayloadError(peerId, ex);
        }
    }
}

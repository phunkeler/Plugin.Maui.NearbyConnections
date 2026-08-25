namespace Plugin.Maui.NearbyConnections;

sealed partial class PlatformNearby
{
    /// <summary>
    /// Name of the cache subdirectory inbound files are staged into. Shared so both platforms
    /// stage to the same place; the absolute path is built per platform, because
    /// <c>FileSystem.CacheDirectory</c> does not resolve on the <c>net10.0</c> target.
    /// </summary>
    internal const string StagingDirectoryName = "nearby-received";

    /// <summary>
    /// The directory inbound files are staged into: app-private, purgeable, and namespaced so
    /// staged files never collide with the host app's own cache files.
    /// </summary>
    /// <remarks>
    /// Declared here and implemented per platform because <c>FileSystem.CacheDirectory</c> does not
    /// resolve on the <c>net10.0</c> target, which compiles this file.
    /// </remarks>
    internal static partial string StagingDirectory { get; }

    internal void WriteDeviceFound(NearbyDevice device)
        => WriteDeviceEvent(device, found: true, nameof(WriteDeviceFound));

    internal void WriteDeviceLost(NearbyDevice device)
        => WriteDeviceEvent(device, found: false, nameof(WriteDeviceLost));

    /// <summary>
    /// The shared found-device handling: log it and write the discovery event. The adapter has
    /// already recorded the peer in <see cref="PeerLookup"/> — that half stays platform-side,
    /// because each SDK hands over a different identifier.
    /// </summary>
    internal void OnDeviceFound(NearbyDevice device)
    {
        LogDeviceFound(device.Id, device.DisplayName);
        WriteDeviceFound(device);
    }

    /// <summary>
    /// The shared lost-device handling, with the connected-device suppression written once: a
    /// connected device that stops advertising is not lost, so its row must not be removed.
    /// </summary>
    internal void OnDeviceLost(string deviceId)
    {
        if (_activeConnections.ContainsKey(deviceId))
        {
            if (PeerLookup.TryGetDevice(deviceId, out var existingDevice))
            {
                LogConnectedDeviceStoppedAdvertising(existingDevice.Id, existingDevice.DisplayName);
            }

            return;
        }

        var device = PeerLookup.Remove(deviceId);

        LogDeviceLost(deviceId, device?.DisplayName);

        if (device is not null)
        {
            WriteDeviceLost(device);
        }
    }

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

    /// <summary>
    /// The shared terminal shape of an outbound file transfer, written once so the catch ladder
    /// cannot drift per platform: awaits <paramref name="completion"/> under the caller's token
    /// and the transfer's inactivity deadline, reports the terminal status from the last observed
    /// progress, cancels the platform's own transfer when either token fires, and observes an
    /// unobserved completion fault so it cannot surface on the finalizer thread.
    /// </summary>
    /// <remarks>
    /// A foreign <see cref="OperationCanceledException"/> — neither the caller's token nor the
    /// inactivity deadline — is wrapped as a transfer failure on both platforms. That settles the
    /// one behavioral divergence the 2026-08-24 review found between the two former catch ladders.
    /// </remarks>
    /// <param name="deviceId">The device the transfer targets, for the timeout and the log.</param>
    /// <param name="transfer">The transfer whose deadline and completion fault this owns.</param>
    /// <param name="completion">Completes when the platform finishes the send, or faults.</param>
    /// <param name="report">Reports the terminal status to the caller's progress.</param>
    /// <param name="cancelPlatformTransfer">Cancels the platform's own transfer.</param>
    /// <param name="cancellationToken">The caller's token.</param>
    internal async Task AwaitFileTransferAsync(
        string deviceId,
        OutgoingTransfer transfer,
        Task completion,
        Action<NearbyTransferStatus> report,
        Action cancelPlatformTransfer,
        CancellationToken cancellationToken)
    {
        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, transfer.InactivityToken);
            using var ctr = linkedCts.Token.Register(cancelPlatformTransfer);

            await completion.WaitAsync(linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            report(NearbyTransferStatus.Canceled);
            throw;
        }
        catch (OperationCanceledException) when (transfer.InactivityToken.IsCancellationRequested)
        {
            report(NearbyTransferStatus.Failure);
            throw TransferInactivityTimeoutException(deviceId);
        }
        catch (Exception ex) when (ex is not NearbyException)
        {
            report(NearbyTransferStatus.Failure);
            LogSendFileFailed(deviceId, null, ex);

            throw new NearbyTransferException(
                $"Failed to send file to device '{deviceId}'.", ex);
        }
        finally
        {
            // A terminal platform update can fault the completion after the caller already left
            // the await on a catch path above, leaving the fault unobserved and surfacing later
            // on the finalizer thread. Observing it here retires that.
            _ = transfer.Completion.Exception;
        }
    }

    /// <summary>
    /// Faults the current advertise channel with a start failure, so the grace window or the pump
    /// observes it. Returns <see langword="false"/> when the fault was dropped — log that.
    /// </summary>
    internal bool TryFaultAdvertiseChannel(Exception exception)
        => _advertiseChannel.Writer.TryComplete(exception);

    /// <summary>The discovery sibling of <see cref="TryFaultAdvertiseChannel"/>.</summary>
    internal bool TryFaultDiscoverChannel(Exception exception)
        => _discoverChannel.Writer.TryComplete(exception);

    internal void WriteConnectionRequest(NearbyConnectionRequest request)
    {
        try
        {
            var channel = _advertiseChannel;
            var written = channel.Writer.TryWrite(request);

            if (!written)
            {
                LogWriteChannelCompleted(nameof(WriteConnectionRequest), request.RemoteDevice.Id);

                // A separate key from the peer's own, on purpose. This rejection needs tracking, so
                // that disposal waits for it, but it does not belong behind that peer's payload
                // work: an inbound copy of several megabytes would otherwise delay telling the
                // remote peer it was rejected, for a reason unrelated to the rejection.
                _ = _workQueue.Enqueue(
                    $"reject:{request.RemoteDevice.Id}",
                    () => RejectUnroutableRequestAsync(request));
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
    /// Registers a pending handshake for <paramref name="deviceId"/> and returns the source the
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
        string deviceId,
        CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<NearbyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);

        _connectionTcs[deviceId] = (tcs, cancellationToken);

        return tcs;
    }

    /// <summary>
    /// Re-points an already-registered handshake at <paramref name="cancellationToken"/>, for the
    /// platform that learns the caller's token only after the request has been surfaced. Leaves a
    /// handshake that has already completed alone.
    /// </summary>
    internal void AttachConnectionTcsToken(string deviceId, CancellationToken cancellationToken)
    {
        if (_connectionTcs.TryGetValue(deviceId, out var entry))
        {
            _connectionTcs.TryUpdate(deviceId, (entry.Tcs, cancellationToken), entry);
        }
    }

    /// <summary>
    /// The shared connection assembly, written once: builds the receive channel, wires the
    /// adapter's platform half into a <see cref="NearbyConnection"/>, and resolves the pending
    /// handshake. Both platforms' terminal success callbacks route through this.
    /// </summary>
    /// <param name="device">The connected remote device.</param>
    /// <param name="sendBytes">The adapter's byte-send for this link.</param>
    /// <param name="sendFile">The adapter's file-send for this link.</param>
    /// <param name="dispose">The adapter's disconnect-and-release for this link.</param>
    internal void CompleteHandshake(
        NearbyDevice device,
        Func<byte[], CancellationToken, Task> sendBytes,
        Func<string, IProgress<NearbyTransferProgress>?, CancellationToken, Task> sendFile,
        Func<ValueTask> dispose)
    {
        var receiveChannel = NewChannel<NearbyPayload>(singleReader: true);

        var connection = new NearbyConnection(device, receiveChannel, sendBytes, sendFile, dispose);

        ResolveConnectionTcs(device.Id, connection);
    }

    internal void ResolveConnectionTcs(string deviceId, NearbyConnection connection)
    {
        try
        {
            if (_connectionTcs.TryRemove(deviceId, out var entry))
            {
                _activeConnections[deviceId] = connection;
                entry.Tcs.TrySetResult(connection);
            }
        }
        catch (Exception ex)
        {
            LogWriteError(nameof(ResolveConnectionTcs), deviceId, ex);
        }
    }

    internal void FaultConnectionTcs(string deviceId, Exception ex)
    {
        try
        {
            if (_connectionTcs.TryRemove(deviceId, out var entry))
            {
                entry.Tcs.TrySetException(ex);
            }
        }
        catch (Exception innerEx)
        {
            LogWriteError(nameof(FaultConnectionTcs), deviceId, innerEx);
        }
    }

    /// <summary>
    /// Releases the platform's bookkeeping for a connection that has ended: removes it from
    /// <c>_activeConnections</c>, ends its receive stream, waits for the work that stream was
    /// feeding, and only then drops the peer's platform handles.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Drain, then release.</b> <c>CompleteReceive</c> only <i>requests</i> cancellation, so
    /// work started for this peer can still be reading the handles
    /// <see cref="PlatformReleaseConnection"/> frees — on Android an inbound copy reads
    /// <c>entry.Payload</c> across an <c>await</c>. Draining the peer's key in
    /// <see cref="KeyedSerialQueue"/> waits for that work first. Ordering is what makes the release
    /// safe. Before this was awaitable the safety rested on a <c>TryRemove</c> race between the two
    /// sides.
    /// </para>
    /// <para>
    /// The drain does not remove the peer's queue entry. A payload that arrives after the peer
    /// disconnects must stay ordered behind a copy that is still writing. Removing the entry here
    /// let that payload start a second copy alongside the first.
    /// </para>
    /// <para>
    /// The drain covers the peer's own key, not the <c>reject:</c> key that
    /// <see cref="WriteConnectionRequest"/> queues an unroutable rejection under. A rejection holds
    /// no handle <see cref="PlatformReleaseConnection"/> frees, so releasing the connection need not
    /// wait for it. Disposal still does, through <see cref="KeyedSerialQueue.DrainAllAsync"/>.
    /// </para>
    /// <para>
    /// Safe to call for a peer with no active connection, and safe to call twice: the
    /// <c>TryRemove</c> guard means <c>CompleteReceive</c> runs at most once per connection.
    /// </para>
    /// </remarks>
    internal async ValueTask ReleaseConnectionAsync(string deviceId)
    {
        if (_activeConnections.TryRemove(deviceId, out var connection))
        {
            connection.CompleteReceive();
        }

        _unobservedWarned.TryRemove(deviceId, out _);

        if (!await _workQueue.DrainAsync(deviceId, DrainTimeout).ConfigureAwait(false))
        {
            LogConnectionDrainTimedOut(deviceId, DrainTimeout.TotalSeconds);
        }

        PlatformReleaseConnection(deviceId);
    }

    /// <summary>
    /// Platform-specific half of <see cref="ReleaseConnectionAsync"/>, run after the peer's
    /// in-flight work has stopped. Implemented on Android to drop the peer's staged payload
    /// handles and on iOS to drop its KVO progress observers.
    /// </summary>
    partial void PlatformReleaseConnection(string deviceId);

    internal void WritePayload(string deviceId, NearbyPayload payload)
    {
        try
        {
            if (!_activeConnections.TryGetValue(deviceId, out var connection))
            {
                LogWritePayloadNoConnection(deviceId);
                return;
            }

            if (!connection.IsBeingConsumed && _unobservedWarned.TryAdd(deviceId, 0))
            {
                LogPayloadArrivedUnobserved(deviceId);
            }

            connection.TryWritePayload(payload);
        }
        catch (Exception ex)
        {
            LogWriteError(nameof(WritePayload), deviceId, ex);
        }
    }

    /// <summary>
    /// Claims a non-colliding destination path for an inbound file by creating it, so the name is
    /// reserved rather than merely observed to be free.
    /// </summary>
    /// <param name="directory">The directory to place the file in. Created if it does not exist.</param>
    /// <param name="fileName">
    /// The remote-supplied file name. Reduced to its file-name component, so a peer cannot steer
    /// the write outside <paramref name="directory"/>.
    /// </param>
    /// <returns>
    /// A writable <see cref="FileStream"/> on the claimed path. The caller owns it and must dispose
    /// it. A caller that moves a file onto the path instead disposes the stream first, then moves
    /// with <c>overwrite: true</c> — the claim stays held across the move.
    /// </returns>
    /// <remarks>
    /// <see cref="FileMode.CreateNew"/> is what makes the reservation atomic: it throws if the name
    /// already exists, and the loop then re-resolves. Two claims can genuinely be in flight in the
    /// same directory at once — Android serialises copies within an endpoint but not across them,
    /// and iOS delegate callbacks for different peers arrive on different threads. Do not weaken
    /// this into a check-then-write against <see cref="ResolveUniqueDestinationPath"/> alone.
    /// </remarks>
    internal static FileStream ClaimUniqueDestinationPath(string directory, string fileName)
    {
        // The name arrives from the remote peer, so strip any directory component it carries.
        fileName = Path.GetFileName(fileName);
        Directory.CreateDirectory(directory);

        while (true)
        {
            var candidate = ResolveUniqueDestinationPath(directory, fileName);

            try
            {
                return new FileStream(candidate, FileMode.CreateNew, FileAccess.Write);
            }
            catch (IOException) when (File.Exists(candidate))
            {
                // A concurrent transfer claimed this name first. Its file now exists, so the next
                // resolve skips past it and the loop makes progress.
            }
        }
    }

    /// <summary>
    /// Deletes a destination file whose copy did not complete. Does nothing when the claim itself
    /// failed, so no path was ever reserved.
    /// </summary>
    /// <remarks>
    /// The name was reserved by creating the file, so an abandoned copy leaves a zero-length or
    /// partial file behind. Left in place it would both look like a delivered payload and make
    /// <see cref="ClaimUniqueDestinationPath"/> skip that name forever after.
    /// </remarks>
    internal void DeletePartialDestination(string? destinationPath)
    {
        if (destinationPath is null)
        {
            return;
        }

        try
        {
            File.Delete(destinationPath);
        }
        catch (Exception ex)
        {
            LogFileDeleteFailed(destinationPath, ex);
        }
    }

    /// <summary>
    /// Deletes every file left in the staging directory. Called once, at session disposal.
    /// </summary>
    /// <remarks>
    /// <see cref="DisposeAsync"/> drains the work queue first, so a copy is normally finished
    /// before this runs. It stays best-effort for the residual case that wait
    /// cannot cover — a copy still writing when the drain times out. Deletes files one at a time
    /// rather than the directory, so one locked file does not strand the rest.
    /// </remarks>
    internal void SweepStagingDirectory(string directory)
    {
        string[] files;

        try
        {
            files = Directory.GetFiles(directory);
        }
        catch (DirectoryNotFoundException)
        {
            return;
        }
        catch (Exception ex)
        {
            LogFileDeleteFailed(directory, ex);
            return;
        }

        foreach (var file in files)
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception ex)
            {
                LogFileDeleteFailed(file, ex);
            }
        }
    }

    /// <summary>
    /// Resolves a non-colliding destination path for an inbound file, appending " (n)" before the
    /// extension when the name is already taken.
    /// </summary>
    /// <remarks>
    /// Both platforms previously combined the destination directory with the sender-supplied name
    /// and overwrote unconditionally, so two peers sending <c>photo.jpg</c> silently clobbered one
    /// another and the app saw only the last one. On its own this is a check, not a reservation:
    /// callers reach it through <see cref="ClaimUniqueDestinationPath"/>, which creates the file to
    /// claim the name and retries when a concurrent transfer wins the race.
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
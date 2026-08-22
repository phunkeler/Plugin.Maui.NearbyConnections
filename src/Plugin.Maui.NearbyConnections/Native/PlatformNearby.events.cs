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
    void DeletePartialDestination(string? destinationPath)
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
    /// Best-effort by design: <see cref="DisposeAsync"/> does not await the Android payload
    /// completion chain, so a cancelled copy may still be writing while this runs. Deletes files
    /// one at a time rather than the directory, so one locked file does not strand the rest.
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
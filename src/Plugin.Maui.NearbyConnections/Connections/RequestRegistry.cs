namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Owns the fact "an inbound request is outstanding for device X": the outstanding set, each
/// request's expiry timer, and the atomic claim that settles accept, reject, and expiry — exactly
/// one caller wins (contract C2, and this fact's row in the C5 table of
/// <c>docs/ARCHITECTURE.md</c> section 4).
/// </summary>
/// <remarks>
/// <para>
/// Expiry effects — the reject, the registry transition, the change publish, and their logging —
/// run inside the <paramref name="onExpired"/> delegate the session injects, so device-state
/// mutation keeps one path. The delegate must not throw; the session's implementation wraps its
/// own body in a catch-and-log.
/// </para>
/// <para>
/// The timers are this component's own fire-and-forget work, owned here rather than by the
/// session's task set: every timer is cancelled by the claim that wins, so none outlives the
/// outstanding set it watches (fail soft — a lost timer race is a no-op, never a double effect).
/// </para>
/// </remarks>
/// <param name="options">The session's options snapshot, read for <see cref="NearbyOptions.InboundRequestTimeout"/>.</param>
/// <param name="timeProvider">The clock the expiry timers run on.</param>
/// <param name="onExpired">Runs the session-side expiry effects for a request whose timer won the claim.</param>
sealed class RequestRegistry(
    NearbyOptions options,
    TimeProvider timeProvider,
    Func<NearbyConnectionRequest, Task> onExpired)
{
    readonly ConcurrentDictionary<string, NearbyConnectionRequest> _outstanding
        = new(StringComparer.Ordinal);
    readonly ConcurrentDictionary<string, CancellationTokenSource> _timers
        = new(StringComparer.Ordinal);

    /// <summary>
    /// Records <paramref name="request"/> as outstanding and arms its expiry timer.
    /// </summary>
    /// <param name="request">The inbound request to track.</param>
    /// <returns>
    /// When the request expires, or <see langword="null"/> when
    /// <see cref="NearbyOptions.InboundRequestTimeout"/> is <see cref="Timeout.InfiniteTimeSpan"/>
    /// and no timer is armed.
    /// </returns>
    public DateTimeOffset? Track(NearbyConnectionRequest request)
    {
        var deviceId = request.RemoteDevice.Id;
        var timeout = options.InboundRequestTimeout;

        // A newer request replaces an older one for the same device, so the older timer must not
        // stay armed against the replacement.
        DisarmTimer(deviceId);
        _outstanding[deviceId] = request;

        if (timeout == Timeout.InfiniteTimeSpan)
        {
            return null;
        }

        var cts = new CancellationTokenSource();
        _timers[deviceId] = cts;
        _ = ExpireAfterAsync(request, timeout, cts.Token);

        return timeProvider.GetUtcNow() + timeout;
    }

    /// <summary>
    /// Atomically claims the outstanding request for <paramref name="deviceId"/>, cancelling its
    /// expiry timer. Exactly one of accept, reject, and expiry wins this claim; the losers see
    /// <see langword="false"/> and must not act on the request.
    /// </summary>
    /// <param name="deviceId">The device whose request to claim.</param>
    /// <param name="request">The claimed request, when this caller won.</param>
    /// <returns><see langword="true"/> when this caller won the claim.</returns>
    public bool TryClaim(string deviceId, [NotNullWhen(true)] out NearbyConnectionRequest? request)
    {
        if (!_outstanding.TryRemove(deviceId, out request))
        {
            return false;
        }

        DisarmTimer(deviceId);
        return true;
    }

    /// <summary>
    /// Atomically claims <paramref name="request"/> itself, cancelling its expiry timer. Unlike
    /// the id-keyed overload, this one never claims a newer request that replaced
    /// <paramref name="request"/> for the same device — a stale request object loses cleanly.
    /// </summary>
    /// <param name="request">The exact request instance to claim.</param>
    /// <returns><see langword="true"/> when this caller won the claim.</returns>
    public bool TryClaim(NearbyConnectionRequest request)
    {
        var deviceId = request.RemoteDevice.Id;

        if (!_outstanding.TryRemove(new KeyValuePair<string, NearbyConnectionRequest>(deviceId, request)))
        {
            return false;
        }

        DisarmTimer(deviceId);
        return true;
    }

    /// <summary>Reports whether a request is outstanding for <paramref name="deviceId"/>.</summary>
    /// <param name="deviceId">The device to check.</param>
    /// <returns><see langword="true"/> when a request is outstanding.</returns>
    public bool Contains(string deviceId) => _outstanding.ContainsKey(deviceId);

    /// <summary>
    /// Snapshots the outstanding requests at the moment of the call — the delivery replay set
    /// (contract C3).
    /// </summary>
    /// <returns>The outstanding requests, possibly empty.</returns>
    public NearbyConnectionRequest[] Snapshot() => [.. _outstanding.Values];

    /// <summary>
    /// Claims every outstanding request at once — the teardown path. All timers are cancelled.
    /// </summary>
    /// <returns>The claimed requests, possibly empty.</returns>
    public NearbyConnectionRequest[] ClaimAll()
    {
        var claimed = new List<NearbyConnectionRequest>();

        foreach (var deviceId in _outstanding.Keys)
        {
            if (TryClaim(deviceId, out var request))
            {
                claimed.Add(request);
            }
        }

        return [.. claimed];
    }

    void DisarmTimer(string deviceId)
    {
        if (_timers.TryRemove(deviceId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    async Task ExpireAfterAsync(
        NearbyConnectionRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(timeout, timeProvider, cancellationToken).ConfigureAwait(false);

            if (!TryClaim(request.RemoteDevice.Id, out var claimed))
            {
                return;
            }

            await onExpired(claimed).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Answered, or the session stopped. Whoever cancelled owns the disposal.
        }
    }
}

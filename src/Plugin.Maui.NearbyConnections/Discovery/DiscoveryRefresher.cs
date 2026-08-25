namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Runs the discovery-refresh duty: on each interval it asks the session to restart the discover
/// pump, waits out the settle window, then evicts the devices the new pass did not re-report.
/// Owns the interval, the settle window, and eviction — nothing else refreshes discovery.
/// </summary>
/// <remarks>
/// <para>
/// The pump restart itself stays with the session: <paramref name="refreshAsync"/> runs under the
/// session's state gate and returns <see langword="false"/> when discovery is no longer running,
/// which ends the loop. The gate never leaves the session. Eviction goes through the registry's
/// own generation API, so the registry stays the one owner of device state.
/// </para>
/// <para>
/// Death policy — degrade loudly: a failed refresh stops this duty and reports through
/// <paramref name="onFailed"/>, while discovery itself continues (the last successful pass keeps
/// serving). See <c>docs/ARCHITECTURE.md</c> section 4, death policies.
/// </para>
/// </remarks>
/// <param name="interval">
/// How often to refresh, or <see langword="null"/> to never refresh — then <see cref="Start"/> is
/// a no-op.
/// </param>
/// <param name="timeProvider">The clock the interval and the settle window run on.</param>
/// <param name="registry">The device registry whose generation API performs the eviction.</param>
/// <param name="refreshAsync">
/// Restarts the discover pump under the session's gate. Returns <see langword="false"/> when
/// discovery stopped and the loop must end.
/// </param>
/// <param name="onFailed">Receives the failure that ended the loop early.</param>
/// <param name="settleWindow">
/// How long after a restart to wait before eviction, or <see langword="null"/> for the default —
/// overridable for tests only.
/// </param>
sealed class DiscoveryRefresher(
    TimeSpan? interval,
    TimeProvider timeProvider,
    DeviceRegistry registry,
    Func<CancellationToken, Task<bool>> refreshAsync,
    Action<Exception> onFailed,
    TimeSpan? settleWindow = null)
{
    /// <summary>
    /// How long a fresh discovery pass gets to re-report the devices still in range before the
    /// ones it did not confirm are evicted.
    /// </summary>
    static readonly TimeSpan s_defaultSettleWindow = TimeSpan.FromSeconds(2);

    readonly TimeSpan _settleWindow = settleWindow ?? s_defaultSettleWindow;

    CancellationTokenSource? _cts;
    Task? _task;

    /// <summary>Starts the refresh loop. A no-op when no interval is configured.</summary>
    public void Start()
    {
        if (interval is not { } configured)
        {
            return;
        }

        var cts = new CancellationTokenSource();

        _cts = cts;
        _task = RunAsync(configured, cts.Token);
    }

    /// <summary>Requests the loop to stop. Pair with <see cref="DrainAsync"/>.</summary>
    public Task CancelAsync() => _cts?.CancelAsync() ?? Task.CompletedTask;

    /// <summary>
    /// Waits for the loop to end and releases its resources. Safe to call with no loop running.
    /// </summary>
    public async Task DrainAsync()
    {
        var cts = _cts;
        var task = _task;

        _cts = null;
        _task = null;

        if (task is not null)
        {
            await task.ConfigureAwait(false);
        }

        cts?.Dispose();
    }

    async Task RunAsync(TimeSpan configured, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(configured, timeProvider);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!await refreshAsync(cancellationToken).ConfigureAwait(false))
                {
                    return;
                }

                await Task.Delay(_settleWindow, timeProvider, cancellationToken).ConfigureAwait(false);

                registry.EvictUnconfirmed();
            }
        }
        catch (OperationCanceledException)
        {
            // Discovery stopped, or the session was disposed.
        }
        catch (Exception ex)
        {
            onFailed(ex);
        }
    }
}

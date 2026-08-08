namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Tears the session down when iOS backgrounds the app, because MultipeerConnectivity does not
/// survive suspension and the plugin would otherwise keep reporting a session iOS has already killed.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why this exists at all.</strong> MPC has no background mode. A normal app is suspended
/// within seconds of backgrounding, and the session fails fast and silently — roughly the first
/// second, reporting <c>MCSessionState.NotConnected</c> with no <c>NSError</c>. Apple's Developer
/// Technical Support is categorical that this is not a bug to work around:
/// <see href="https://developer.apple.com/forums/thread/11964">forum 11964</see>. Without this
/// observer the plugin reports <see cref="NearbyDeviceStatus.Connected"/> for a connection that no
/// longer exists — a zombie state the consumer has no way to detect.
/// </para>
/// <para>
/// <strong>Why the advertiser and browser stop too, not just the session.</strong> Apple's
/// prescribed handling is to disconnect on background and rebuild on return. In practice
/// <c>MCNearbyServiceAdvertiser</c>/<c>MCNearbyServiceBrowser</c> instances are often still live
/// objects after resume and appear to carry on scanning — but that is observed behaviour, not a
/// documented guarantee, and it is exactly the kind of unsupported reliance the same DTS engineer
/// warns breaks on a future OS. Stopping them keeps <see cref="INearbyConnections.IsAdvertising"/> and
/// <see cref="INearbyConnections.IsDiscovering"/> honest: while suspended nothing is scanning, so
/// reporting <see langword="true"/> would be a second zombie state alongside the first.
/// </para>
/// <para>
/// <strong>Nothing restarts on foreground.</strong> Teardown is an explicit, observable transition —
/// connections raise <see cref="INearbyConnections.ConnectionDropped"/> and the flags go
/// <see langword="false"/> — and restarting is the app's call, consistent with the plugin's
/// "nothing starts on its own" contract. There is no MPC reconnect primitive in any case: recovery
/// means re-advertising and re-inviting, and the retry policy is app-specific.
/// </para>
/// </remarks>
sealed partial class AppLifecycleObserver : IDisposable
{
    readonly NearbyConnectionsImplementation _session;
    readonly ILogger _logger;

    NSObject? _backgroundRegistration;
    int _disposeGuard;

    internal AppLifecycleObserver(NearbyConnectionsImplementation session, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(logger);

        _session = session;
        _logger = logger;

        // DidEnterBackground, not WillResignActive: the latter also fires for transient
        // interruptions that never suspend the app — the app switcher, a control-centre pull, an
        // incoming call banner — and tearing a live connection down for those would be far more
        // disruptive than the bug being fixed.
        _backgroundRegistration = NSNotificationCenter.DefaultCenter.AddObserver(
            UIApplication.DidEnterBackgroundNotification,
            OnDidEnterBackground);
    }

    void OnDidEnterBackground(NSNotification notification)
    {
        LogTearingDownForBackground();

        // Fire-and-forget with an explicit continuation, deliberately: this runs on the main thread
        // inside a UIKit notification callback, where iOS gives the app only seconds before
        // suspension. Blocking on StopAsync here would risk a watchdog kill, and there is no
        // caller to hand the task to.
        _ = TearDownAsync();
    }

    async Task TearDownAsync()
    {
        try
        {
            // StopAsync, not a bespoke teardown: it already stops advertising and discovery,
            // disposes every connection (so ConnectionDropped is raised through the one existing
            // path), rejects outstanding inbound requests so remote peers are not left hanging, and
            // clears Devices. Reimplementing that here would mean a second teardown path to keep
            // correct. The session remains usable — the app can start again on foreground.
            await _session.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Nothing awaits this task, so an unlogged failure here would be invisible.
            LogBackgroundTearDownFailed(ex);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeGuard, 1) != 0)
        {
            return;
        }

        if (_backgroundRegistration is not null)
        {
            NSNotificationCenter.DefaultCenter.RemoveObserver(_backgroundRegistration);
            _backgroundRegistration.Dispose();
            _backgroundRegistration = null;
        }
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "App entered the background. Tearing the nearby session down, because MultipeerConnectivity " +
            "does not survive suspension. Connections are dropped and advertising/discovery stop; start again " +
            "when the app returns to the foreground.")]
    partial void LogTearingDownForBackground();

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Failed to tear the nearby session down after the app entered the background. " +
            "Session state may not reflect that iOS has already ended the connection.")]
    partial void LogBackgroundTearDownFailed(Exception exception);
}

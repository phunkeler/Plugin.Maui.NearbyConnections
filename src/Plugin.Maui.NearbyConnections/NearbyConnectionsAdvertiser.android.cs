using System;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Manages advertising for nearby connections.
/// </summary>
public partial class NearbyConnectionsAdvertiser : Java.Lang.Object
{
    public Task PlatformStartAdvertising(IAdvertisingOptions options, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Platform-specific advertising start logic must be implemented.");

    public Task PlatformStopAdvertising(CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Platform-specific advertising stop logic must be implemented.");

#pragma warning disable CA1822 // Mark members as static
    public void PlatformIsDisposing() { }
#pragma warning restore CA1822 // Mark members as static
}

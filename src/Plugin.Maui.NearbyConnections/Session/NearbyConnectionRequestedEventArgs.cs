namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Carries the remote device that has asked to connect.
/// </summary>
/// <remarks>
/// Respond with <see cref="INearbySession.AcceptAsync"/> or
/// <see cref="INearbySession.RejectAsync"/>. To accept every request automatically, call
/// <c>AcceptAsync</c> from the handler — but consider showing the user who is connecting first.
/// </remarks>
public sealed class NearbyConnectionRequestedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new <see cref="NearbyConnectionRequestedEventArgs"/>.
    /// </summary>
    /// <param name="device">The device requesting the connection.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="device"/> is <see langword="null"/>.</exception>
    public NearbyConnectionRequestedEventArgs(NearbyDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        Device = device;
    }

    /// <summary>
    /// Gets the device requesting the connection. Its
    /// <see cref="NearbyDevice.Status"/> is <see cref="NearbyDeviceStatus.RequestReceived"/> until
    /// the request is answered.
    /// </summary>
    public NearbyDevice Device { get; }
}

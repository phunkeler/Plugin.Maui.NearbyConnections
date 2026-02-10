namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Manages the collection and lifecycle state of nearby devices.
/// </summary>
interface INearbyDeviceManager
{
    /// <summary>
    /// Gets a snapshot of all currently tracked devices.
    /// </summary>
    IReadOnlyList<NearbyDevice> Devices { get; }

    /// <summary>
    /// Tracks a newly discovered device or returns the existing one if already tracked.
    /// Sets state to <see cref="NearbyDeviceState.Discovered"/>.
    /// </summary>
    /// <param name="id">The unique device identifier.</param>
    /// <param name="displayName">The user-friendly display name.</param>
    /// <returns>The tracked <see cref="NearbyDevice"/> instance.</returns>
    NearbyDevice DeviceFound(string id, string? displayName);

    /// <summary>
    /// Gets on existing tracked device or adds a new one with the specified initial state.
    /// </summary>
    /// <param name="id">The unique device identifier.</param>
    /// <param name="displayName">The user-friendly display name (used only when creating).</param>
    /// <param name="initialState">The initial <see cref="NearbyDeviceState"/> for a newly added device.</param>
    /// <returns></returns>
    NearbyDevice GetOrAddDevice(string id, string? displayName, NearbyDeviceState initialState);

    /// <summary>
    /// Removes a device that is no longer discoverable.
    /// </summary>
    /// <param name="id">The unique device identifier.</param>
    /// <returns>The removed <see cref="NearbyDevice"/>, or <see langword="null"/> if not found.</returns>
    NearbyDevice? DeviceLost(string id);

    /// <summary>
    /// Transitions a tracked device to the specified state.
    /// </summary>
    /// <param name="id">The unique device identifier.</param>
    /// <param name="state">The new <see cref="NearbyDeviceState"/>.</param>
    /// <returns>The updated <see cref="NearbyDevice"/>, or <see langword="null"/> if not found.</returns>
    NearbyDevice? SetState(string id, NearbyDeviceState state);

    /// <summary>
    /// Removes a device that has disconnected.
    /// </summary>
    /// <param name="id">The unique device identifier.</param>
    /// <returns>The removed <see cref="NearbyDevice"/>, or <see langword="null"/> if not found.</returns>
    NearbyDevice? DeviceDisconnected(string id);

    /// <summary>
    /// Removes all tracked devices.
    /// </summary>
    void Clear();
}

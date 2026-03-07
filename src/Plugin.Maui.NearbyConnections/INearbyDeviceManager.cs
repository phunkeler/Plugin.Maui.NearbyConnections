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
    /// For a new device, sets state to Discovered. For an already-tracked device, only updates LastSeen.
    /// </summary>
    /// <param name="id">The unique device identifier.</param>
    /// <param name="displayName">The user-friendly display name.</param>
    /// <returns>The tracked <see cref="NearbyDevice"/> instance.</returns>
    NearbyDevice DeviceFound(string id, string? displayName);

    /// <summary>
    /// Gets an existing tracked device or adds a new one with the specified initial state.
    /// </summary>
    /// <param name="id">The unique device identifier.</param>
    /// <param name="displayName">The user-friendly display name (used only when creating).</param>
    /// <param name="initialState">The initial <see cref="NearbyDeviceState"/> for a newly added device.</param>
    /// <returns>Returns the newly tracked <see cref="NearbyDevice"/> or the existing one, if the key exists.</returns>
    NearbyDevice GetOrAddDevice(string id, string? displayName, NearbyDeviceState initialState);

    /// <summary>
    /// Removes a tracked device by identifier.
    /// Use this for both "lost during discovery" and "session disconnected" scenarios;
    /// callers are responsible for firing the appropriate event.
    /// </summary>
    /// <param name="id">The unique device identifier.</param>
    /// <returns>The removed <see cref="NearbyDevice"/>, or <see langword="null"/> if not found.</returns>
    NearbyDevice? RemoveDevice(string id);

    /// <summary>
    /// Transitions a tracked device to the specified state.
    /// </summary>
    /// <param name="id">The unique device identifier.</param>
    /// <param name="state">The new <see cref="NearbyDeviceState"/>.</param>
    /// <returns>The updated <see cref="NearbyDevice"/>, or <see langword="null"/> if not found.</returns>
    NearbyDevice? SetState(string id, NearbyDeviceState state);

    /// <summary>
    /// Gets a tracked device by its identifier.
    /// </summary>
    /// <param name="id">The unique device identifier.</param>
    /// <param name="device">The tracked <see cref="NearbyDevice" />, or <see langword="null"/> if not found.</param>
    /// <returns><see langword="true" /> is the device was found; otherwise, <see langword="false"/>.</returns>
    bool TryGetDevice(string id, [NotNullWhen(true)] out NearbyDevice? device);

    /// <summary>
    /// Removes all tracked devices.
    /// </summary>
    void Clear();
}

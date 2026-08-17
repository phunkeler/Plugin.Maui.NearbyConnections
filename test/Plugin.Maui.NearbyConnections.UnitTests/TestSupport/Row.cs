namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// A stand-in for the kind of row view model a consumer projects devices onto with
/// <see cref="NearbyDeviceCollection{TRow}"/>: it carries state of its own
/// (<see cref="UpdateCount"/>) that a rebuilt row would lose, which is what makes row reuse
/// observable in a test.
/// </summary>
/// <param name="device">The device this row shows.</param>
sealed class Row(NearbyDevice device)
{
    /// <summary>The device this row currently shows.</summary>
    public NearbyDevice Device { get; private set; } = device;

    /// <summary>The shown device's id.</summary>
    public string Id => Device.Id;

    /// <summary>The shown device's status.</summary>
    public NearbyDeviceStatus Status => Device.Status;

    /// <summary>
    /// How many times this row was handed a newer snapshot. Stays at zero on a row that was
    /// replaced rather than updated.
    /// </summary>
    public int UpdateCount { get; private set; }

    /// <summary>Hands this row a newer snapshot of the same device.</summary>
    /// <param name="updated">The newer snapshot.</param>
    public void Update(NearbyDevice updated)
    {
        Device = updated;
        UpdateCount++;
    }
}

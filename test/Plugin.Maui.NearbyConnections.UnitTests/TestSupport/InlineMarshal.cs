namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// Runs marshalled actions inline and counts them, so a test can assert that mutation went through
/// the callback rather than around it.
/// </summary>
/// <remarks>
/// <see cref="NearbyDeviceCollection"/> takes an <c>Action&lt;Action&gt;</c> rather than a
/// dispatcher interface, so this stands in for <c>Dispatcher.Dispatch</c> by running the work
/// immediately on the calling thread.
/// </remarks>
sealed class InlineMarshal
{
    /// <summary>How many actions were marshalled.</summary>
    public int Count { get; private set; }

    public void Run(Action action)
    {
        Count++;
        action();
    }
}

namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// Records whether the container has resolved the session yet.
/// </summary>
/// <remarks>
/// <para>
/// Registered as the factory for <see cref="TimeProvider"/>, which <c>AddNearby</c> resolves while
/// constructing <see cref="INearby"/>. Observing the factory therefore turns "has the session been
/// built?" into a fact a test can assert on — which is what makes construction <em>ordering</em>
/// testable.
/// </para>
/// <para>
/// The factory, not a clock read: the session captures a <see cref="TimeProvider"/> during
/// construction but does not necessarily read it, so a witness that only counted
/// <c>GetUtcNow</c> calls would report false long after the session existed.
/// </para>
/// </remarks>
sealed class ConstructionWitness
{
    /// <summary>Whether the container has built the session.</summary>
    public bool WasResolved { get; private set; }

    /// <summary>The factory to register for <see cref="TimeProvider"/>.</summary>
    public TimeProvider Resolve(IServiceProvider services)
    {
        WasResolved = true;
        return TimeProvider.System;
    }
}

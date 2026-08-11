namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// Polls for a condition instead of sleeping for a fixed interval.
/// </summary>
/// <remarks>
/// Every delivery path in this library is a channel drained on a background task, so an assertion
/// made immediately after an operation races the pump. A bare <c>Task.Delay</c> either flakes under
/// load or wastes its full budget on every run; polling returns as soon as the state is reachable.
/// </remarks>
static class Wait
{
    /// <summary>
    /// Returns once <paramref name="condition"/> holds, or after roughly one second. Does not throw
    /// on timeout — the assertion that follows is what reports the failure, with a useful message.
    /// </summary>
    public static async Task UntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(10);
        }
    }
}

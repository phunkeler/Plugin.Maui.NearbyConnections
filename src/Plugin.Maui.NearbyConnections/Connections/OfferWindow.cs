namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// The two named numbers of the one-deadline model: an inbound connection offer lives for the
/// window its initiator declared on the wire, assumed when nothing was declared, clamped by the
/// local trust bound. Both are internal constants, not <see cref="NearbyOptions"/> knobs — the
/// same precedent as the drain deadlines: bounds that exist for safety take no configuration.
/// </summary>
static class OfferWindow
{
    internal static readonly TimeSpan s_default = TimeSpan.FromSeconds(30);

    internal static readonly TimeSpan s_max = TimeSpan.FromMinutes(5);

    internal static TimeSpan Clamp(TimeSpan declared)
        => declared == Timeout.InfiniteTimeSpan || declared > s_max
            ? s_max
            : declared < TimeSpan.Zero
                ? TimeSpan.Zero
                : declared;
}

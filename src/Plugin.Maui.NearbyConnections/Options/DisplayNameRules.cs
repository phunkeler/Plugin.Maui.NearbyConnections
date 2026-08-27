namespace Plugin.Maui.NearbyConnections;

static class DisplayNameRules
{
    internal const int MaxBytes = 63;

    const string Reference =
        "See https://developer.apple.com/documentation/multipeerconnectivity/mcpeerid/init(displayname:).";

    internal static void Validate(string? displayName, List<string> failures)
    {
        if (string.IsNullOrEmpty(displayName))
        {
            failures.Add(
                $"{nameof(NearbyOptions.DisplayName)} must not be null or empty. On iOS it is passed directly as MCPeerID's " +
                "displayName, which rejects both and raises a native exception that cannot be " +
                "caught. " + Reference);
            return;
        }

        var bytes = Encoding.UTF8.GetByteCount(displayName);

        if (bytes > MaxBytes)
        {
            failures.Add(
                $"{nameof(NearbyOptions.DisplayName)} is {bytes} UTF-8 bytes; the limit is {MaxBytes} on " +
                "every platform — it is Apple's MCPeerID cap on iOS, and the connection request's " +
                "wire budget elsewhere. Note the limit counts bytes rather than characters, so a " +
                "name outside the ASCII range reaches it sooner than its length suggests. " + Reference);
        }
    }
}

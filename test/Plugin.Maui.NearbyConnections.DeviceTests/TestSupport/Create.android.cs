using Microsoft.Extensions.Logging.Abstractions;

namespace Plugin.Maui.NearbyConnections.DeviceTests;

static partial class Create
{
    /// <summary>The real platform type on Android, wired with the default <see cref="PeerLookup"/>.</summary>
    /// <param name="options">Options to wire the platform with, or <see langword="null"/> for the suite defaults.</param>
    /// <returns>The platform under test.</returns>
    public static PlatformNearby PlatformNearby(NearbyOptions? options = null)
        => new(TimeProvider.System, options ?? DefaultOptions(), NullLogger.Instance, new PeerLookup());

    /// <summary>A transfer update built via the SDK's Builder, the only construction path GMS exposes.</summary>
    /// <param name="payloadId">The payload the update refers to.</param>
    /// <param name="status">One of the <see cref="PayloadTransferUpdate.Status"/> values.</param>
    /// <param name="total">Total bytes in the transfer.</param>
    /// <param name="transferred">Bytes transferred so far.</param>
    /// <returns>The transfer update.</returns>
    public static PayloadTransferUpdate TransferUpdate(
        long payloadId, int status, long total = 3, long transferred = 3)
        => new PayloadTransferUpdate.Builder()
            .SetPayloadId(payloadId)
            .SetStatus(status)
            .SetTotalBytes(total)
            .SetBytesTransferred(transferred)
            .Build();

    /// <summary>
    /// An inbound file payload backed by a real file in the cache directory. The copy path reads it
    /// through <c>ContentResolver</c>, so the file must exist on disk for the copy to succeed.
    /// </summary>
    /// <param name="contents">Bytes to write to the backing file.</param>
    /// <param name="fileName">Name of the backing file, unique per test.</param>
    /// <returns>The payload, owning a descriptor on the backing file.</returns>
    public static Payload FilePayload(byte[] contents, string fileName)
    {
        var path = Path.Combine(Microsoft.Maui.Storage.FileSystem.CacheDirectory, fileName);
        File.WriteAllBytes(path, contents);

        var file = new Java.IO.File(path);

        return Payload.FromFile(file)!;
    }

    /// <summary>
    /// A connection resolution carrying <paramref name="statusCode"/>. Google marks this ctor
    /// deprecated but still ships it at the pinned binding version, so the suppression is scoped to
    /// this one line: a binding bump that removes the ctor fails the build here.
    /// </summary>
    /// <param name="statusCode">One of the <see cref="ConnectionsStatusCodes"/> values.</param>
    /// <returns>The resolution.</returns>
    public static ConnectionResolution Resolution(int statusCode = ConnectionsStatusCodes.StatusOk)
#pragma warning disable CS0618
        => new(new Statuses(statusCode));
#pragma warning restore CS0618

    /// <summary>
    /// The incoming-connection info GMS hands to <c>OnConnectionInitiated</c>. Same
    /// deprecated-but-shipped ctor situation as <see cref="Resolution"/>.
    /// </summary>
    /// <param name="displayName">The remote device's display name, as GMS reports it.</param>
    /// <returns>The connection info.</returns>
    public static ConnectionInfo ConnectionInfo(string displayName = "Alice")
#pragma warning disable CS0618
        => new(displayName, "auth-token", isIncomingConnection: true);
#pragma warning restore CS0618

    /// <summary>
    /// The discovery info GMS hands to <c>OnEndpointFound</c>. Same deprecated-but-shipped ctor
    /// situation as <see cref="Resolution"/>.
    /// </summary>
    /// <param name="displayName">The remote device's display name, as GMS reports it.</param>
    /// <returns>The discovered endpoint info.</returns>
    public static DiscoveredEndpointInfo DiscoveredEndpointInfo(string displayName = "Alice")
#pragma warning disable CS0618
        => new(ServiceId, displayName);
#pragma warning restore CS0618

    /// <summary>
    /// A handshake pending on the advertise channel: the platform has a registered
    /// <c>_connectionTcs</c> entry for <paramref name="id"/> awaiting a connection result.
    /// </summary>
    /// <param name="platform">The platform to register the pending handshake on.</param>
    /// <param name="id">The endpoint id the handshake is keyed by.</param>
    /// <param name="displayName">The remote device's display name, as GMS reports it.</param>
    /// <returns>The source the platform will resolve or fault.</returns>
    public static TaskCompletionSource<NearbyConnection> PendingHandshake(
        PlatformNearby platform, string id = "endpoint-1", string displayName = "Alice")
    {
        var tcs = new TaskCompletionSource<NearbyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);

        platform.Peers.Record(id, displayName);
        platform._connectionTcs[id] = (tcs, CancellationToken.None);

        return tcs;
    }

    /// <summary>
    /// A live connection, established by driving the real platform success callback rather than by
    /// reaching into the connection's own state.
    /// </summary>
    /// <param name="platform">The platform whose callback establishes the connection.</param>
    /// <param name="displayName">The remote device's display name, as GMS reports it.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The connection, and the platform-side endpoint id it is keyed by.</returns>
    public static async Task<(NearbyConnection Connection, string Id)> ConnectedAsync(
        PlatformNearby platform,
        string displayName,
        CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<NearbyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
        const string id = "endpoint-1";

        platform.Peers.Record(id, displayName);
        platform._connectionTcs[id] = (tcs, CancellationToken.None);
        platform.OnConnectionResult(id, Resolution());

        return (await tcs.Task.WaitAsync(cancellationToken), id);
    }
}

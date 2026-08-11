using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// Constructs the types under test. Call sites read <c>Create.Device("a")</c>, which names where the
/// value came from without the test file having to carry a factory of its own.
/// </summary>
/// <remarks>
/// Everything here builds a <em>real</em> object — these are not test doubles. The only stand-in in
/// this suite is <see cref="FakeNearby"/>, which implements the <see cref="IPlatformNearby"/> seam.
/// </remarks>
static class Create
{
    /// <summary>A discovered device. Defaults to the state a device is in when first seen.</summary>
    public static NearbyDevice Device(
        string id = "peer-1",
        string? displayName = null,
        NearbyDeviceStatus status = NearbyDeviceStatus.Visible)
        => new(id, displayName ?? id) { Status = status };

    /// <summary>
    /// A connection whose send and dispose callbacks are inert, so a test can drive
    /// <see cref="NearbyConnection.ReceiveAsync"/> without a platform behind it.
    /// </summary>
    public static NearbyConnection Connection(
        NearbyDevice? device = null,
        Channel<NearbyPayload>? receiveChannel = null,
        Func<byte[], CancellationToken, Task>? sendBytes = null,
        Func<string, IProgress<NearbyTransferProgress>?, CancellationToken, Task>? sendFile = null,
        Func<ValueTask>? dispose = null)
        => new(
            device ?? Device(),
            receiveChannel ?? Channel.CreateUnbounded<NearbyPayload>(
                new UnboundedChannelOptions { SingleReader = true, SingleWriter = false }),
            sendBytes: sendBytes ?? ((_, _) => Task.CompletedTask),
            sendFile: sendFile ?? ((_, _, _) => Task.CompletedTask),
            dispose: dispose ?? (() => ValueTask.CompletedTask));

    /// <summary>
    /// The session under test, over a <see cref="FakeNearby"/> platform. Takes no dispatcher: every
    /// member of <see cref="INearby"/> is callable from any thread.
    /// </summary>
    public static NearbyImplementation Session(
        FakeNearby platform,
        NearbyOptions? options = null,
        TimeProvider? timeProvider = null)
        => timeProvider is null
            ? new(platform, options ?? new NearbyOptions(), NullLogger.Instance)
            : new(platform, options ?? new NearbyOptions(), NullLogger.Instance, timeProvider);

    /// <summary>
    /// The real platform type. Its <c>Platform*</c> members throw on <c>net10.0</c>, so tests drive
    /// the channel bridge and the members that do not reach the SDK.
    /// </summary>
    public static PlatformNearby PlatformNearby(
        FakeTimeProvider? timeProvider = null,
        NearbyOptions? options = null)
        => new(
            timeProvider ?? new FakeTimeProvider(),
            options ?? new NearbyOptions(),
            NullLogger.Instance);

    /// <summary>
    /// The inactivity timeout <see cref="Transfer"/> uses unless a test overrides it. Exposed so a
    /// test can advance the clock relative to the deadline without restating the number.
    /// </summary>
    public const double TransferTimeoutSeconds = 10;

    /// <summary>An outgoing transfer wired to a fake clock, so the deadline can be driven directly.</summary>
    public static OutgoingTransfer Transfer(
        FakeTimeProvider time,
        IProgress<NearbyTransferProgress>? progress = null,
        TimeSpan? timeout = null)
        => new(progress, timeout ?? TimeSpan.FromSeconds(TransferTimeoutSeconds), time);

    /// <summary>One progress report, as the platform would raise it mid-transfer.</summary>
    public static NearbyTransferProgress ProgressUpdate(NearbyTransferStatus status, long bytes = 0)
        => new(payloadId: 1, bytesTransferred: bytes, totalBytes: 100, status);
}

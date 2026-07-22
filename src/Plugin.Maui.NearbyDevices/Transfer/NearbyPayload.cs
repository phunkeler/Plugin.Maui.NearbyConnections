namespace Plugin.Maui.NearbyDevices;

/// <summary>
/// Represents data to be sent to or received from a nearby device.
/// </summary>
public abstract record NearbyPayload;

/// <summary>
/// A payload containing raw bytes. Limited to 32 KB on Android; use
/// <see cref="FilePayload"/> for larger data.
/// </summary>
/// <param name="Data">The bytes to send or that were received.</param>
/// <remarks>
/// byte[] in a record uses reference equality — two instances wrapping identical bytes are not ==.
/// Consumers who need value equality should compare Data.AsSpan().SequenceEqual(other.Data).
/// </remarks>
public sealed record BytesPayload(byte[] Data) : NearbyPayload;

/// <summary>
/// A payload representing a received file. Consumers are responsible for deleting <see cref="FileResult"/> when finished with it.
/// </summary>
/// <param name="FileResult">The received file.</param>
public sealed record FilePayload(FileResult FileResult) : NearbyPayload;

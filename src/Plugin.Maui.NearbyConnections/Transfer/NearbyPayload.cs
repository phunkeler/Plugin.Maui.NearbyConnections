namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Represents data sent to, or received from, a nearby device.
/// </summary>
/// <remarks>
/// This is the base type for all payloads. The library produces and accepts two concrete payload
/// types: <see cref="BytesPayload"/> and <see cref="FilePayload"/>.
/// </remarks>
/// <seealso cref="BytesPayload"/>
/// <seealso cref="FilePayload"/>
public abstract record NearbyPayload;

/// <summary>
/// Represents a payload that contains raw bytes.
/// </summary>
/// <param name="Data">The bytes that were sent or received.</param>
/// <remarks>
/// <para>
/// On Android, byte payloads are limited to 32 KB. Use <see cref="FilePayload"/> for larger data.
/// </para>
/// <para>
/// Because <see cref="Data"/> is an array, this record compares it by reference: two instances
/// wrapping identical byte sequences are not equal. To compare the contents, use
/// <see cref="MemoryExtensions.SequenceEqual{T}(ReadOnlySpan{T}, ReadOnlySpan{T})"/> on the
/// <see cref="Data"/> values.
/// </para>
/// </remarks>
public sealed record BytesPayload(byte[] Data) : NearbyPayload;

/// <summary>
/// Represents a payload that contains a file.
/// </summary>
/// <param name="FileResult">The file that was sent or received.</param>
/// <remarks>
/// A received file is written to
/// <see cref="NearbyConnectionsOptions.ReceivedFilesDirectory"/>. The consuming application owns
/// the file from that point on and is responsible for deleting it when it is no longer needed.
/// </remarks>
public sealed record FilePayload(FileResult FileResult) : NearbyPayload;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Represents a payload that contains raw bytes.
/// </summary>
/// <param name="Data">The bytes that were sent or received.</param>
/// <remarks>
/// <para>
/// On Android, byte payloads are limited to 32 KB. Use <see cref="NearbyFilePayload"/> for larger
/// data.
/// </para>
/// <para>
/// Because <see cref="Data"/> is an array, this record compares it by reference: two instances
/// wrapping identical byte sequences are not equal. To compare the contents, use
/// <see cref="MemoryExtensions.SequenceEqual{T}(ReadOnlySpan{T}, ReadOnlySpan{T})"/> on the
/// <see cref="Data"/> values.
/// </para>
/// </remarks>
public sealed record NearbyBytesPayload(byte[] Data) : NearbyPayload;

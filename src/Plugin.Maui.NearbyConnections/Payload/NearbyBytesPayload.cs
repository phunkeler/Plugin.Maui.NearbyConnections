namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Represents a payload that contains raw bytes.
/// </summary>
/// <param name="Data">The bytes that were sent or received.</param>
/// <remarks>
/// <para>
/// On Android, byte payloads are limited to 32 KB. Use <see cref="NearbyFilePayload"/> instead for
/// larger data.
/// </para>
/// <para>
/// <see cref="Data"/> is an array, so the compiler-generated equality for this record compares it
/// by reference: two instances wrapping identical byte sequences are not equal. To compare the
/// contents instead, use
/// <see cref="MemoryExtensions.SequenceEqual{T}(ReadOnlySpan{T}, ReadOnlySpan{T})"/> on the two
/// <see cref="Data"/> values.
/// </para>
/// </remarks>
/// <seealso cref="NearbyFilePayload"/>
public sealed record NearbyBytesPayload(byte[] Data) : NearbyPayload;

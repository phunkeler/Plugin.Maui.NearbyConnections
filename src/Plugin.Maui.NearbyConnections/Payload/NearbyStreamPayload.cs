namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Represents an inbound named byte stream — live or unknown-length data, arriving through the
/// same <see cref="NearbyConnection.ReceiveAsync(CancellationToken)"/> loop as every other
/// payload. The remote device opened it with
/// <see cref="NearbyConnection.OpenStreamAsync(string, CancellationToken)"/>.
/// </summary>
/// <param name="Stream">
/// The readable stream. The consumer owns it: read it to the end or dispose it. It ends when the
/// remote writer disposes its half, or when the connection drops.
/// </param>
/// <param name="Name">
/// The name the remote device opened the stream with. Remote-chosen input — display it with the
/// same care as a display name.
/// </param>
/// <remarks>
/// <see cref="Stream"/> is compared by reference by this record's compiler-generated equality,
/// like <see cref="NearbyBytesPayload.Data"/>.
/// </remarks>
/// <seealso cref="NearbyBytesPayload"/>
/// <seealso cref="NearbyFilePayload"/>
public sealed record NearbyStreamPayload(Stream Stream, string Name) : NearbyPayload;

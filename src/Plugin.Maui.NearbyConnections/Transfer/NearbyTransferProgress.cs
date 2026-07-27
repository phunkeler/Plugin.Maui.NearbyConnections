namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Represents the current status of a data transfer.
/// </summary>
public enum NearbyTransferStatus
{
    /// <summary>The transfer is in progress.</summary>
    InProgress,

    /// <summary>The transfer completed successfully.</summary>
    Success,

    /// <summary>The transfer failed due to an error.</summary>
    Failure,

    /// <summary>The transfer was cancelled.</summary>
    Canceled,
}

/// <summary>
/// Reports the progress of an ongoing or completed data transfer.
/// </summary>
/// <param name="payloadId">Platform-assigned identifier for the payload.</param>
/// <param name="bytesTransferred">Number of bytes transferred so far.</param>
/// <param name="totalBytes">
/// Total size of the payload in bytes, or <c>-1</c> if the size is not known in advance
/// (e.g. live streams).
/// </param>
/// <param name="status">The current transfer status.</param>
public sealed class NearbyTransferProgress(
    long payloadId,
    long bytesTransferred,
    long totalBytes,
    NearbyTransferStatus status)
{
    /// <summary>
    /// Gets the platform-assigned identifier for the payload.
    /// </summary>
    public long PayloadId { get; } = payloadId;

    /// <summary>
    /// Gets the number of bytes transferred so far.
    /// </summary>
    public long BytesTransferred { get; } = bytesTransferred;

    /// <summary>
    /// Gets the total size of the payload in bytes, or <c>-1</c> if unknown.
    /// </summary>
    public long TotalBytes { get; } = totalBytes;

    /// <summary>
    /// Gets the current transfer status.
    /// </summary>
    public NearbyTransferStatus Status { get; } = status;

    /// <summary>
    /// Gets the transfer progress as a value between 0.0 and 1.0,
    /// or <see langword="null"/> if the total size is not known.
    /// </summary>
    public double? Fraction => TotalBytes > 0
        ? (double)BytesTransferred / TotalBytes
        : null;
}

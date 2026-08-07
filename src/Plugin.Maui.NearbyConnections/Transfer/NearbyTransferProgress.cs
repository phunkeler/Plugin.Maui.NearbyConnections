namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Specifies the status of a data transfer.
/// </summary>
public enum NearbyTransferStatus
{
    /// <summary>The transfer is in progress.</summary>
    InProgress,

    /// <summary>The transfer completed successfully.</summary>
    Success,

    /// <summary>The transfer failed because of an error.</summary>
    Failure,

    /// <summary>The transfer was canceled.</summary>
    Canceled,
}

/// <summary>
/// Represents the progress of an ongoing or completed payload transfer.
/// </summary>
/// <param name="payloadId">The platform-assigned identifier for the payload.</param>
/// <param name="bytesTransferred">The number of bytes transferred so far.</param>
/// <param name="totalBytes">
/// The total size of the payload in bytes, or <c>-1</c> if the size is not known in advance.
/// </param>
/// <param name="status">The status of the transfer.</param>
/// <remarks>
/// Instances are reported through the <see cref="IProgress{T}"/> provider supplied to a
/// <c>SendAsync</c> overload, or through
/// <see cref="NearbyConnection.InboundProgress"/> for incoming transfers.
/// </remarks>
public sealed class NearbyTransferProgress(
    long payloadId,
    long bytesTransferred,
    long totalBytes,
    NearbyTransferStatus status)
{
    /// <summary>
    /// Gets the platform-assigned identifier for the payload.
    /// </summary>
    /// <value>An identifier that distinguishes this transfer from others on the same connection.</value>
    public long PayloadId { get; } = payloadId;

    /// <summary>
    /// Gets the number of bytes transferred so far.
    /// </summary>
    /// <value>The count of bytes transferred at the time this update was reported.</value>
    public long BytesTransferred { get; } = bytesTransferred;

    /// <summary>
    /// Gets the total size of the payload.
    /// </summary>
    /// <value>
    /// The total size of the payload in bytes, or <c>-1</c> if the size is not known in advance.
    /// </value>
    public long TotalBytes { get; } = totalBytes;

    /// <summary>
    /// Gets the status of the transfer.
    /// </summary>
    /// <value>One of the <see cref="NearbyTransferStatus"/> values.</value>
    public NearbyTransferStatus Status { get; } = status;

    /// <summary>
    /// Gets the proportion of the transfer that has completed.
    /// </summary>
    /// <value>
    /// A value between 0.0 and 1.0, or <see langword="null"/> if <see cref="TotalBytes"/> is not
    /// known. Bind to this value to drive a progress indicator.
    /// </value>
    public double? Fraction => TotalBytes > 0
        ? (double)BytesTransferred / TotalBytes
        : null;
}

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Represents data to be sent to or received from a nearby device.
/// </summary>
public abstract class NearbyPayload;

/// <summary>
/// A payload containing raw bytes. Limited to 32 KB on Android; use
/// <see cref="FilePayload"/> for larger data.
/// </summary>
/// <param name="data">The bytes to send or that were received.</param>
public sealed class BytesPayload(byte[] data) : NearbyPayload
{
    /// <summary>
    /// Gets the raw byte data.
    /// </summary>
    public byte[] Data { get; } = data;
}

/// <summary>
/// A payload representing a received file. Consumers are responsible for deleting <see cref="FileResult"/> when finished with it.
/// </summary>
/// <param name="fileResult">The received file.</param>
public sealed class FilePayload(FileResult fileResult) : NearbyPayload
{
    /// <summary>
    /// Gets the received file.
    /// </summary>
    public FileResult FileResult { get; } = fileResult;
}
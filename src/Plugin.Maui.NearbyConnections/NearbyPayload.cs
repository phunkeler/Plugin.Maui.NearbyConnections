namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Represents data to be sent to or received from a nearby device.
/// </summary>
public abstract class NearbyPayload { }

/// <summary>
/// A payload containing raw bytes. Limited to 32 KB on Android; use
/// <see cref="FilePayload"/> or <see cref="StreamPayload"/> for larger data.
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
/// A payload backed by a file on disk. Suitable for large transfers.
/// The file must be accessible at send time; on the receiving end <see cref="FilePath"/>
/// is a platform-managed temporary location.
/// </summary>
/// <param name="filePath">Absolute path to the file.</param>
public sealed class FilePayload(string filePath) : NearbyPayload
{
    /// <summary>
    /// Gets the absolute path to the file.
    /// </summary>
    public string FilePath { get; } = filePath;
}

/// <summary>
/// A payload backed by a <see cref="Stream"/>. Suitable for live or generated data
/// of unknown length. The factory is invoked once by the platform implementation when
/// the transfer begins.
/// </summary>
/// <param name="streamFactory">
/// A factory that returns the <see cref="Stream"/> to read from (send) or write to (receive).
/// </param>
/// <param name="name">An optional name identifying the stream, used on iOS.</param>
public sealed class StreamPayload(Func<Stream> streamFactory, string name = "") : NearbyPayload
{
    /// <summary>
    /// Gets the factory used to obtain the underlying <see cref="Stream"/>.
    /// </summary>
    public Func<Stream> StreamFactory { get; } = streamFactory;

    /// <summary>
    /// Gets the optional name identifying this stream.
    /// </summary>
    public string Name { get; } = name;
}

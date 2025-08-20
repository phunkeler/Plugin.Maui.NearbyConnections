namespace Plugin.Maui.NearbyConnections.Models;

/// <summary>
/// Represents the type of data payload.
/// </summary>
public enum DataPayloadType
{
    /// <summary>
    /// Raw byte data or text content.
    /// </summary>
    Bytes,

    /// <summary>
    /// File resource to be sent.
    /// </summary>
    File,

    /// <summary>
    /// Stream data to be sent.
    /// </summary>
    Stream
}

/// <summary>
/// Represents a data payload that can be sent to peers.
/// </summary>
public abstract class DataPayload
{
    /// <summary>
    /// Gets the type of this payload.
    /// </summary>
    public abstract DataPayloadType Type { get; }

    /// <summary>
    /// Gets an optional name/identifier for this payload.
    /// </summary>
    public string? Name { get; init; }
}

/// <summary>
/// Represents a byte array data payload.
/// </summary>
/// <remarks>
///     <para>
///         Android: The size is limited by "Connections.MAX_BYTES_DATA_SIZE".
///     </para>
/// </remarks>
public class BytesPayload : DataPayload
{
    /// <inheritdoc/>
    public override DataPayloadType Type => DataPayloadType.Bytes;

    /// <summary>
    /// Gets the byte data to send.
    /// </summary>
    public required byte[] Data { get; init; }

    /// <summary>
    /// Creates a BytesPayload from a UTF-8 string.
    /// </summary>
    /// <param name="text">The text to convert to bytes.</param>
    /// <param name="name">Optional name for the payload.</param>
    /// <returns>A BytesPayload containing the UTF-8 encoded text.</returns>
    public static BytesPayload FromText(string text, string? name = null)
    {
        return new BytesPayload
        {
            Data = System.Text.Encoding.UTF8.GetBytes(text),
            Name = name
        };
    }
}

/// <summary>
/// Represents a file data payload.
/// </summary>
public class FilePayload : DataPayload
{
    /// <inheritdoc/>
    public override DataPayloadType Type => DataPayloadType.File;

    /// <summary>
    /// Gets the file path to send.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Gets the file size in bytes, if known.
    /// </summary>
    public long? FileSizeBytes { get; init; }

    /// <summary>
    /// Creates a FilePayload from a file path.
    /// </summary>
    /// <param name="filePath">The path to the file to send.</param>
    /// <param name="name">Optional name for the payload. If not provided, uses the filename.</param>
    /// <returns>A FilePayload for the specified file.</returns>
    /// <exception cref="FileNotFoundException">Thrown if the file does not exist.</exception>
    public static FilePayload FromFile(string filePath, string? name = null)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        var fileInfo = new FileInfo(filePath);
        return new FilePayload
        {
            FilePath = filePath,
            Name = name ?? fileInfo.Name,
            FileSizeBytes = fileInfo.Length
        };
    }
}

/// <summary>
/// Represents a stream data payload.
/// </summary>
public class StreamPayload : DataPayload
{
    /// <inheritdoc/>
    public override DataPayloadType Type => DataPayloadType.Stream;

    /// <summary>
    /// Gets the stream to send data from.
    /// </summary>
    public required Stream Stream { get; init; }

    /// <summary>
    /// Gets the expected stream size in bytes, if known.
    /// </summary>
    public long? StreamSizeBytes { get; init; }
}
namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Represents a payload that contains a file.
/// </summary>
/// <param name="FileResult">The file that was sent or received.</param>
/// <remarks>
/// A received file is written to <see cref="NearbyOptions.ReceivedFilesDirectory"/>, and ownership
/// passes to the consuming application at that point — it is responsible for deleting the file when
/// it is no longer needed.
/// </remarks>
/// <seealso cref="NearbyBytesPayload"/>
public sealed record NearbyFilePayload(FileResult FileResult) : NearbyPayload;

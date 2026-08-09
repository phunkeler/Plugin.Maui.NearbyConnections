namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Represents a payload that contains a file.
/// </summary>
/// <param name="FileResult">The file that was sent or received.</param>
/// <remarks>
/// A received file is written to
/// <see cref="NearbyOptions.ReceivedFilesDirectory"/>. The consuming application owns
/// the file from that point on and is responsible for deleting it when it is no longer needed.
/// </remarks>
public sealed record NearbyFilePayload(FileResult FileResult) : NearbyPayload;

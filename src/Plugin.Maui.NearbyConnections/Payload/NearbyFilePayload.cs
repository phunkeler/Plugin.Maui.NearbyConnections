namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Represents a payload that contains a file.
/// </summary>
/// <param name="FileResult">The file that was sent or received.</param>
/// <remarks>
/// <para>
/// A received file is staged in app-private, operating-system-purgeable storage, and it is yours
/// from the moment this payload reaches your receive loop. Call
/// <see cref="MoveTo(string)"/> to keep it, or ignore it to discard it.
/// </para>
/// <para>
/// A file you do not move is deleted when the session is disposed, and the operating system may
/// reclaim the staging directory before then. Move what you want to keep.
/// </para>
/// <para>
/// <c>FileName</c> and <c>ContentType</c> derive from what the sending device supplied. Treat both
/// as untrusted input: validate the content before acting on the declared type.
/// </para>
/// </remarks>
/// <seealso cref="NearbyBytesPayload"/>
public sealed record NearbyFilePayload(FileResult FileResult) : NearbyPayload
{
    /// <summary>
    /// Moves the received file out of staging to a location this application owns.
    /// </summary>
    /// <param name="destinationPath">
    /// The full path to move the file to, file name included. Missing parent directories are
    /// created.
    /// </param>
    /// <returns>A <see cref="FileResult"/> for the file at its new location.</returns>
    /// <remarks>
    /// <para>
    /// The move consumes the staged file, so nothing is left to clean up — copying the stream
    /// instead keeps the staged original alive until the session is disposed. Use the returned
    /// <see cref="FileResult"/> afterwards: this payload's own still points at the staging path,
    /// which no longer exists.
    /// </para>
    /// <para>
    /// Within the application sandbox this is a rename and returns immediately, which is why it is
    /// synchronous. Call it once per payload — concurrent calls for the same payload are not
    /// supported.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="destinationPath"/> is <see langword="null"/>.</exception>
    /// <exception cref="IOException">
    /// A file already exists at <paramref name="destinationPath"/>, or the file has already been
    /// moved or purged from staging.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">The application cannot write to the destination.</exception>
    public FileResult MoveTo(string destinationPath)
    {
        ArgumentNullException.ThrowIfNull(destinationPath);

        if (Path.GetDirectoryName(destinationPath) is { Length: > 0 } parent)
        {
            Directory.CreateDirectory(parent);
        }

        File.Move(FileResult.FullPath, destinationPath);

        return new FileResult(destinationPath);
    }
}

namespace NearbyChat.Services;

public interface IThumbnailService
{
    /// <summary>
    /// Returns a thumbnail <see cref="ImageSource"/> for the given video file path,
    /// or <see langword="null"/> if the thumbnail could not be generated.
    /// </summary>
    Task<ImageSource?> GetVideoThumbnailAsync(string filePath, CancellationToken cancellationToken = default);
}

using Android.Graphics;
using Android.Media;
using Android.Provider;

namespace NearbyChat.Services;

public static class ThumbnailService
{
    /// <summary>
    /// Returns a thumbnail for the given video file path, or <see langword="null"/> if the
    /// thumbnail could not be generated.
    /// </summary>
    /// <remarks>
    /// <b>Never throws except on cancellation.</b> The caller is the inbound payload loop, and an
    /// exception escaping here ends that loop for the whole connection — every later payload from
    /// that peer would be lost. A thumbnail is decoration, so every failure degrades to
    /// <see langword="null"/> instead.
    /// </remarks>
    public static Task<ImageSource?> GetVideoThumbnailAsync(string filePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var bitmap = CreateThumbnail(filePath);

            if (bitmap is null)
            {
                return Task.FromResult<ImageSource?>(null);
            }

            var tempFilePath = SaveBitmapToCache(bitmap);

            if (string.IsNullOrWhiteSpace(tempFilePath))
            {
                return Task.FromResult<ImageSource?>(null);
            }

            return Task.FromResult<ImageSource?>(ImageSource.FromFile(tempFilePath));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best-effort, and deliberately broad. Android media failures arrive as
            // Java.Lang.Exception (corrupt file, unsupported codec), but SaveBitmapToCache does
            // managed file I/O and throws IOException or UnauthorizedAccessException — a full
            // cache directory used to escape this catch and kill the receive loop.
            System.Diagnostics.Debug.WriteLine($"Video thumbnail failed for '{filePath}': {ex}");

            return Task.FromResult<ImageSource?>(null);
        }
    }

    static Bitmap? CreateThumbnail(string filePath)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            return ThumbnailUtils.CreateVideoThumbnail(new Java.IO.File(filePath), new Android.Util.Size(200, 200), null);
        }
        else
        {
            return ThumbnailUtils.CreateVideoThumbnail(filePath, ThumbnailKind.MiniKind);
        }
    }

    static string SaveBitmapToCache(Bitmap bitmap, string extension = ".png")
    {
        var fileName = $"thumb_{Guid.NewGuid():N}{extension}";
        var filePath = System.IO.Path.Combine(FileSystem.CacheDirectory, fileName);

        using (var fileStream = new FileStream(filePath, FileMode.Create))
        {
            bitmap.Compress(Bitmap.CompressFormat.Png!, 90, fileStream);
        }

        return filePath;
    }
}

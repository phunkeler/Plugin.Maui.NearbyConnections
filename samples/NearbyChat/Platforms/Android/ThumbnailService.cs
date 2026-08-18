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
        catch (Java.Lang.Exception)
        {
            // Thumbnail extraction is best-effort: Android media failures
            // (corrupt file, unsupported codec) degrade to "no thumbnail"
            // rather than failing the incoming message.
            return Task.FromResult<ImageSource?>(null);
        }
    }

    public static Bitmap? CreateThumbnail(string filePath)
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

    public static string SaveBitmapToCache(Bitmap bitmap, string extension = ".png")
    {
        var fileName = $"thumb_{Guid.NewGuid():N}{extension}";
        var filePath = System.IO.Path.Combine(FileSystem.CacheDirectory, fileName);

        using (var fileStream = new FileStream(filePath, FileMode.Create))
        {
            bitmap.Compress(Bitmap.CompressFormat.Png!, 90, fileStream);
        }

        return filePath; // Use this path for your PhotoAttachment
    }
}

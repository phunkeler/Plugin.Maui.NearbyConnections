using Android.Graphics;
using Android.Media;
using Android.Provider;

namespace NearbyChat.Services;

public class ThumbnailService : IThumbnailService
{
    public Task<ImageSource> GetVideoThumbnailAsync(string filePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var bitmap = CreateThumbnail(filePath);

            if (bitmap is null)
            {
                return Task.FromResult(ImageSource.FromFile(""));
            }

            var tempFilePath = SaveBitmapToCache(bitmap);

            if (string.IsNullOrWhiteSpace(tempFilePath))
            {
                return Task.FromResult(ImageSource.FromFile(""));
            }

            return Task.FromResult(ImageSource.FromFile(tempFilePath));

        }
        catch
        {
            return Task.FromResult(ImageSource.FromFile(""));
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

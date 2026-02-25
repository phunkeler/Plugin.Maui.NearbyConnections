using Android.Graphics;
using Android.Media;

namespace NearbyChat.Services;

public class ThumbnailService : IThumbnailService
{
    public Task<ImageSource?> GetVideoThumbnailAsync(string filePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var retriever = new MediaMetadataRetriever();
            retriever.SetDataSource(filePath);

            // Seek to 1 second (in microseconds); fall back to frame 0 if the video is shorter
            var bitmap = retriever.GetFrameAtTime(1_000_000) ?? retriever.GetFrameAtTime(0);

            if (bitmap is null)
            {
                return Task.FromResult<ImageSource?>(null);
            }

            using var stream = new MemoryStream();
            bitmap.Compress(Bitmap.CompressFormat.Png!, quality: 100, stream);
            bitmap.Recycle();

            var bytes = stream.ToArray();
            return Task.FromResult<ImageSource?>(ImageSource.FromStream(() => new MemoryStream(bytes)));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return Task.FromResult<ImageSource?>(null);
        }
    }
}

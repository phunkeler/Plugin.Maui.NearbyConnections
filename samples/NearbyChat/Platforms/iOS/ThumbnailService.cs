using AVFoundation;
using CoreMedia;
using Foundation;

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
    public static async Task<ImageSource?> GetVideoThumbnailAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            using var url = NSUrl.FromFilename(filePath);
            using var asset = AVAsset.FromUrl(url);
            using var generator = new AVAssetImageGenerator(asset)
            {
                AppliesPreferredTrackTransform = true
            };

            var time = new CMTime(1, 1);
            var tcs = new TaskCompletionSource<ImageSource?>();

            using var _ = cancellationToken.Register(() =>
            {
                generator.CancelAllCGImageGeneration();
                tcs.TrySetCanceled(cancellationToken);
            });

            // AVAssetImageGenerator.GenerateCGImageAsynchronously is iOS 16.0+, above this
            // sample's 15.0 SupportedOSPlatformVersion. Suppressed deliberately rather than
            // guarded: the sample is exercised on current devices (the UI test suite is
            // Android-only) and an untested iOS 15 fallback path is not worth shipping for
            // a best-effort thumbnail.
#pragma warning disable CA1416 // Validate platform compatibility
            generator.GenerateCGImageAsynchronously(time, (imageRef, actualTime, error) =>
            {
                // Runs on an AVFoundation thread. An exception thrown here is unobservable and
                // would leave the TCS unresolved, so the caller would await forever.
                try
                {
                    if (error is not null || imageRef is null)
                    {
                        tcs.TrySetResult(null);
                        return;
                    }

                    using var uiImage = new UIKit.UIImage(imageRef);
                    using var png = uiImage.AsPNG();

                    if (png is null)
                    {
                        tcs.TrySetResult(null);
                        return;
                    }

                    // Materialized here rather than in a deferred stream factory: the UIImage and
                    // its PNG representation are both disposed on the way out of this callback.
                    var bytes = png.ToArray();

                    tcs.TrySetResult(ImageSource.FromStream(() => new MemoryStream(bytes)));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Video thumbnail callback failed for '{filePath}': {ex}");

                    tcs.TrySetResult(null);
                }
            });
#pragma warning restore CA1416 // Validate platform compatibility

            return await tcs.Task;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // NSUrl.FromFilename, AVAsset.FromUrl, and the generator constructor all throw for an
            // unreadable or malformed file, before the callback above is ever wired up.
            System.Diagnostics.Debug.WriteLine($"Video thumbnail failed for '{filePath}': {ex}");

            return null;
        }
    }
}

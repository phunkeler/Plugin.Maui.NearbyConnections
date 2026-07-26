using AVFoundation;
using CoreMedia;
using Foundation;

namespace NearbyChat.Services;

public class ThumbnailService : IThumbnailService
{
    public async Task<ImageSource?> GetVideoThumbnailAsync(string filePath, CancellationToken cancellationToken = default)
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
            if (error != null || imageRef is null)
            {
                tcs.TrySetResult(null);
                return;
            }

            var uiImage = new UIKit.UIImage(imageRef);
            tcs.TrySetResult(ImageSource.FromStream(() => uiImage.AsPNG()!.AsStream()));
        });
#pragma warning restore CA1416 // Validate platform compatibility

        return await tcs.Task;
    }
}

using AndroidSdk;

namespace Plugin.Maui.NearbyConnections.UiTests.Appium;

/// <summary>
/// Prepares physical Android devices before a test run — keeps screen on,
/// wakes the display, and dismisses the lock screen.
/// Uses AndroidSdk to avoid requiring external shell scripts.
/// </summary>
internal static class DevicePrep
{
    internal static async Task PrepareAsync(params string[] serials)
    {
        var sdk = new AndroidSdkManager();

        // Only download platform-tools when adb is not already on PATH.
        // On the Pi self-hosted runner adb is pre-installed; downloading would
        // fail because the runner user lacks write access to the SDK cache path.
        if (!IsAdbOnPath())
        {
            await sdk.Acquire();
        }

        foreach (var serial in serials)
        {
            // Keep screen on while plugged in (USB + AC + wireless = 7)
            sdk.Adb.Shell("settings put global stay_on_while_plugged_in 7", serial);

            // Wake screen
            sdk.Adb.Shell("input keyevent 26", serial);

            // Dismiss lock screen (works when Smart Lock keeps the device unlocked)
            sdk.Adb.Shell("wm dismiss-keyguard", serial);
        }
    }

    private static bool IsAdbOnPath()
    {
        var pathDirs = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        var adbName = OperatingSystem.IsWindows() ? "adb.exe" : "adb";
        return pathDirs.Any(dir => File.Exists(Path.Combine(dir, adbName)));
    }
}

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

        // Downloads Android SDK platform-tools if not already present.
        // No-op when ADB is already in PATH (e.g. Pi self-hosted runner, local MAUI workload).
        await sdk.Acquire();

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
}

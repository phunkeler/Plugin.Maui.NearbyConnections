using Android.App;
using Android.Content.PM;
using Android.OS;

namespace Plugin.Maui.NearbyConnections.DeviceTests.Runner;

// Name is explicit because DeviceRunners' Android launcher starts "<package>/.MainActivity".
// Without it the Java binding generator emits a hashed name (crc64<hash>.MainActivity), the launch
// is rejected with "Activity class does not exist", and the run fails at "Starting the
// application..." with an empty logcat that looks like a hang rather than a bad launch target.
[Activity(Name = "com.phunkeler.nearbyconnections.devicetests.MainActivity", Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
}

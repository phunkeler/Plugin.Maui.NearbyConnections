namespace NearbyChat.Services;

public class NearbyPermissions : INearbyPermissions
{
    public async Task<PermissionStatus> EnsureGrantedAsync()
    {
        var bluetooth = await EnsureAsync<Permissions.Bluetooth>();
        if (bluetooth is not PermissionStatus.Granted)
        {
            return bluetooth;
        }

        // Below API 31 Permissions.Bluetooth resolves to an empty permission set and returns Granted
        // without prompting for anything, so location is what actually gates discovery there. From 31
        // BLUETOOTH_SCAN covers it, and NEARBY_WIFI_DEVICES only exists from 33.
        if (!OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            return await EnsureAsync<Permissions.LocationWhenInUse>();
        }

        return OperatingSystem.IsAndroidVersionAtLeast(33)
            ? await EnsureAsync<Permissions.NearbyWifiDevices>()
            : PermissionStatus.Granted;
    }

    // Check before requesting: RequestAsync on an already-granted permission is wasted work, and on
    // Android the rationale has to be shown BEFORE the prompt or it never gets seen.
    static async Task<PermissionStatus> EnsureAsync<T>()
        where T : Permissions.BasePermission, new()
    {
        var status = await Permissions.CheckStatusAsync<T>();
        if (status is PermissionStatus.Granted)
        {
            return status;
        }

        if (Permissions.ShouldShowRationale<T>())
        {
            await Shell.Current.DisplayAlertAsync(
                "Permission needed",
                "Nearby needs Bluetooth and Wi-Fi access to find devices around you.",
                "OK");
        }

        return await Permissions.RequestAsync<T>();
    }
}

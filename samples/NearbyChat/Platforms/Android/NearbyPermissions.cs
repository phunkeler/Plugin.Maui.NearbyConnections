namespace NearbyChat.Services;

public class NearbyPermissions : INearbyPermissions
{
    public async Task<bool> EnsureGrantedAsync()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            var bluetooth = await Permissions.RequestAsync<Permissions.Bluetooth>();
            var nearbyWifiDevices = await Permissions.RequestAsync<Permissions.NearbyWifiDevices>();
            return bluetooth is PermissionStatus.Granted && nearbyWifiDevices is PermissionStatus.Granted;
        }

        var bluetoothStatus = await Permissions.RequestAsync<Permissions.Bluetooth>();
        var locationStatus = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        return bluetoothStatus is PermissionStatus.Granted && locationStatus is PermissionStatus.Granted;
    }
}

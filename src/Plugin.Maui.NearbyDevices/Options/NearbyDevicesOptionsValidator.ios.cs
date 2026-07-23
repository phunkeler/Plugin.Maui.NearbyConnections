namespace Plugin.Maui.NearbyDevices;

sealed partial class NearbyDevicesOptionsValidator
{
    static partial void PlatformValidate(NearbyDevicesOptions options, List<string> failures)
    {
        if (options.ServiceId == "_UNSET")
        {
            failures.Add(
                "ServiceId has not been set. On iOS, ServiceId is passed directly as " +
                "MCNearbyServiceAdvertiser/MCNearbyServiceBrowser's serviceType, which Apple " +
                "requires to be 1-15 characters long identifying the network protocol " +
                "(for example 'xamarin-txtchat') — it is NOT a Bonjour '_name._tcp' service " +
                "type; that format is only used for the app's Info.plist NSBonjourServices entries.");
        }
        else if (options.ServiceId.Length is < 1 or > 15)
        {
            failures.Add(
                $"ServiceId '{options.ServiceId}' is {options.ServiceId.Length} characters long. " +
                "On iOS, ServiceId is passed directly as MCNearbyServiceAdvertiser/" +
                "MCNearbyServiceBrowser's serviceType, which Apple requires to be between 1 and " +
                "15 characters long (for example 'xamarin-txtchat').");
        }
    }
}
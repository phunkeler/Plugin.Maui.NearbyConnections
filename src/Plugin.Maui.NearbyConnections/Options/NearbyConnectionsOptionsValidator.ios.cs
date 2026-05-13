namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyConnectionsOptionsValidator
{
    static partial void PlatformValidate(NearbyConnectionsOptions options, List<string> failures)
    {
        if (options.ServiceId == "_UNSET._tcp")
        {
            failures.Add(
                "ServiceId has not been set. On iOS, ServiceId must be a valid Bonjour service type " +
                "in the form '_<name>._tcp' or '_<name>._udp' (for example '_mygame._tcp'), " +
                "matching an entry declared in the app's Info.plist under NSBonjourServices.");
        }
    }
}
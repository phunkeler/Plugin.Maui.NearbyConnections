using Plugin.Maui.NearbyConnections;

namespace NearbyChat.Services;

/// <summary>
/// Turns a <see cref="NearbyAvailability"/> result into something to show the user.
/// </summary>
public static class NearbyAvailabilityText
{
    /// <summary>
    /// Describes everything the user has to resolve before nearby connectivity can start.
    /// </summary>
    /// <remarks>
    /// Every applicable flag is reported at once, so this lists them all rather than only the first:
    /// telling someone to turn Bluetooth on, and then — after they do — that a permission is also
    /// missing, is two round trips for one problem.
    /// </remarks>
    public static string Describe(NearbyAvailability availability)
    {
        if (availability is NearbyAvailability.Ready)
        {
            return string.Empty;
        }

        var causes = new List<string>();

        if (availability.HasFlag(NearbyAvailability.UnsupportedPlatform))
        {
            return "This device does not support nearby connections.";
        }

        if (availability.HasFlag(NearbyAvailability.InvalidConfiguration))
        {
            return "This app's nearby service identifier is not valid. This is a bug in the app.";
        }

        if (availability.HasFlag(NearbyAvailability.PlayServicesUnavailable))
        {
            causes.Add("update Google Play services");
        }

        if (availability.HasFlag(NearbyAvailability.BluetoothDisabled))
        {
            causes.Add("turn Bluetooth on");
        }

        if (availability.HasFlag(NearbyAvailability.WifiDisabled))
        {
            causes.Add("turn Wi-Fi on for faster transfers");
        }

        if (availability.HasFlag(NearbyAvailability.MissingPermissions))
        {
            causes.Add("grant the nearby permissions");
        }

        return causes.Count switch
        {
            0 => "Nearby is unavailable for an unknown reason.",
            1 => $"Please {causes[0]}, then try again.",
            _ => $"Please {string.Join(", ", causes[..^1])} and {causes[^1]}, then try again.",
        };
    }
}

namespace Plugin.Maui.NearbyConnections.Advertise;

/// <summary>
/// iOS-specific extensions for NearbyAdvertisement.
/// </summary>
public sealed partial class NearbyAdvertisement
{
    /// <summary>
    /// Converts the advertisement to an NSDictionary for iOS MultipeerConnectivity.
    /// </summary>
    /// <returns>NSDictionary with all non-null values, or null if no data.</returns>
    public NSDictionary? ToNSDictionary()
    {
        var dict = ToDictionary();

        if (dict.Count == 0)
        {
            return null;
        }

        // Convert Dictionary<string, string> to NSDictionary
        var keys = dict.Keys.Select(k => (NSString)k).ToArray();
        var values = dict.Values.Select(v => (NSString)v).ToArray();

        return NSDictionary<NSString, NSString>.FromObjectsAndKeys(values, keys);
    }

    /// <summary>
    /// Creates an advertisement from an NSDictionary (from iOS discovery info).
    /// </summary>
    /// <param name="nsDictionary">NSDictionary containing advertisement data.</param>
    /// <returns>Deserialized advertisement, or null if data is invalid.</returns>
    public static NearbyAdvertisement? FromNSDictionary(NSDictionary? nsDictionary)
    {
        if (nsDictionary is null || nsDictionary.Count == 0)
            return null;

        // Convert NSDictionary to Dictionary<string, string>
        var dict = new Dictionary<string, string>();

        foreach (var key in nsDictionary.Keys)
        {
            if (key is NSString nsKey && nsDictionary[key] is NSString nsValue)
            {
                dict[nsKey.ToString()] = nsValue.ToString();
            }
        }

        return FromDictionary(dict);
    }
}

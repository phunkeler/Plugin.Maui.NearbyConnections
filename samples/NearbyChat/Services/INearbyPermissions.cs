namespace NearbyChat.Services;

/// <summary>
/// Ensures the runtime permissions required for advertising/discovery are granted.
/// </summary>
public interface INearbyPermissions
{
    /// <summary>
    /// Requests any missing platform permissions required for Nearby Connections /
    /// Multipeer Connectivity.
    /// </summary>
    /// <returns>
    /// <see cref="PermissionStatus.Granted"/> when everything required is held. Any other value
    /// identifies what stopped it, so callers can distinguish a user who declined this time from
    /// one who has permanently denied and can only be helped by a trip to system settings.
    /// </returns>
    Task<PermissionStatus> EnsureGrantedAsync();
}

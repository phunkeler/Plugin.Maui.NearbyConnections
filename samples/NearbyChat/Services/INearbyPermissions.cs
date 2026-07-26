namespace NearbyChat.Services;

/// <summary>
/// Ensures the runtime permissions required for advertising/discovery are granted.
/// </summary>
public interface INearbyPermissions
{
    /// <summary>
    /// Requests any missing platform permissions required for Nearby Connections /
    /// Multipeer Connectivity, returning whether all required permissions are granted.
    /// </summary>
    Task<bool> EnsureGrantedAsync();
}

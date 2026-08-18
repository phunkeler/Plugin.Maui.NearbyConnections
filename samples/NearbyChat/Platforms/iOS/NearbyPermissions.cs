namespace NearbyChat.Services;

public static class NearbyPermissions
{
    // iOS has no runtime permission API for Multipeer Connectivity — the
    // local-network access prompt (NSLocalNetworkUsageDescription) is shown
    // by the OS itself the first time the app actually starts advertising/
    // browsing, not via Microsoft.Maui.ApplicationModel.Permissions.
    public static Task<PermissionStatus> EnsureGrantedAsync() => Task.FromResult(PermissionStatus.Granted);
}

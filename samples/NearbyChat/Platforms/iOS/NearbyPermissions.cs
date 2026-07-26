namespace NearbyChat.Services;

public class NearbyPermissions : INearbyPermissions
{
    // iOS has no runtime permission API for Multipeer Connectivity — the
    // local-network access prompt (NSLocalNetworkUsageDescription) is shown
    // by the OS itself the first time the app actually starts advertising/
    // browsing, not via Microsoft.Maui.ApplicationModel.Permissions.
    public Task<bool> EnsureGrantedAsync() => Task.FromResult(true);
}

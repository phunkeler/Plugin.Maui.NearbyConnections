using System.ComponentModel;

namespace NearbyChat.Services;

/// <summary>
/// Tracks the number of active connections across both the advertiser and discoverer
/// streams for the lifetime of the app, so any page can show an authoritative count.
/// </summary>
public interface IConnectionTracker : INotifyPropertyChanged
{
    /// <summary>
    /// Gets the number of currently connected devices.
    /// </summary>
    int Count { get; }
}

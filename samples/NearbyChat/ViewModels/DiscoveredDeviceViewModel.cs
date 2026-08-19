using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

/// <summary>
/// A discovered, not-yet-connected device row.
/// </summary>
public partial class DiscoveredDeviceViewModel : NearbyDeviceViewModel
{
    readonly INearby _nearby;
    readonly ILogger _logger;

    public DiscoveredDeviceViewModel(
        NearbyDevice device,
        INearby nearby,
        ILogger logger)
        : base(device)
    {
        ArgumentNullException.ThrowIfNull(nearby);
        ArgumentNullException.ThrowIfNull(logger);

        _nearby = nearby;
        _logger = logger;
    }

    /// <summary>
    /// Why the last connection attempt failed, or <see langword="null"/> if none has.
    /// </summary>
    /// <remarks>
    /// Shown on the row rather than as an alert, so the message stays attached to the device it
    /// concerns and does not interrupt a user who is connecting to several devices in turn.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFailed))]
    public partial string? FailureReason { get; set; }

    /// <summary>
    /// Whether the last connection attempt failed and the row is showing its reason.
    /// </summary>
    public bool HasFailed => FailureReason is not null;

    /// <summary>
    /// Whether a handshake is in flight, projected from <see cref="NearbyDevice.Status"/>.
    /// </summary>
    /// <remarks>
    /// No longer a settable flag with manual unwind on every failure path: the session owns the
    /// transition and resets the device if the handshake fails, so the row cannot get stuck
    /// spinning after a rejected connection.
    /// </remarks>
    public bool IsConnecting => Device.Status is NearbyDeviceStatus.Connecting;

    [RelayCommand(IncludeCancelCommand = true)]
    async Task Connect(CancellationToken cancellationToken)
    {
        FailureReason = null;

        try
        {
            await _nearby.ConnectAsync(Device, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The user cancelled. Say nothing — they already know, and the session has returned
            // the device to Visible so the row is connectable again.
        }
        catch (NearbyConnectionTimeoutException ex)
        {
            LogConnectFailed(_logger, Device.Id, ex);

            FailureReason = "No answer. Check the other device is showing your request.";
        }
        catch (NearbyException ex)
        {
            // Rejected, out of range, or a platform error. The session has already returned the
            // device to Visible, so the row stays connectable and only needs a reason.
            LogConnectFailed(_logger, Device.Id, ex);

            FailureReason = "Could not connect. The device declined or moved out of range.";
        }
    }

    protected override void OnDeviceChanged()
    {
        OnPropertyChanged(nameof(IsConnecting));

        // A fresh handshake clears a stale reason: the row is showing what is happening now, not
        // what failed last time.
        if (IsConnecting)
        {
            FailureReason = null;
        }
    }

    [LoggerMessage(
        EventId = 1,
        EventName = nameof(LogConnectFailed),
        Level = LogLevel.Error,
        Message = "Connecting to {DeviceId} failed.")]
    static partial void LogConnectFailed(ILogger logger, string deviceId, Exception exception);
}

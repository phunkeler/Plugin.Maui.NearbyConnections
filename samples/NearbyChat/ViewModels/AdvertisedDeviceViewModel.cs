using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

/// <summary>
/// A device awaiting a response to its inbound connection request.
/// </summary>
public partial class AdvertisedDeviceViewModel : NearbyDeviceViewModel
{
    readonly Func<string, NearbyConnectionRequest?> _requestFor;
    readonly ILogger _logger;

    public AdvertisedDeviceViewModel(
        NearbyDevice device,
        Func<string, NearbyConnectionRequest?> requestFor,
        ILogger logger)
        : base(device)
    {
        ArgumentNullException.ThrowIfNull(requestFor);
        ArgumentNullException.ThrowIfNull(logger);

        _requestFor = requestFor;
        _logger = logger;
    }

    /// <summary>
    /// Why the last response to this request failed, or <see langword="null"/> if none has.
    /// </summary>
    /// <remarks>
    /// Shown on the row rather than as an alert, so the message stays attached to the device it
    /// concerns and does not interrupt a user answering several requests in turn.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFailed))]
    public partial string? FailureReason { get; set; }

    /// <summary>
    /// Whether the last response failed and the row is showing its reason.
    /// </summary>
    public bool HasFailed => FailureReason is not null;

    [RelayCommand(IncludeCancelCommand = true)]
    async Task Accept(CancellationToken cancellationToken)
    {
        FailureReason = null;

        if (_requestFor(Device.Id) is not { } request)
        {
            FailureReason = "The request expired before it was accepted.";
            return;
        }

        try
        {
            await request.AcceptAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The user cancelled. Say nothing — they already know.
        }
        catch (NearbyRequestExpiredException ex)
        {
            LogAcceptFailed(_logger, Device.Id, ex);

            FailureReason = "The request expired before it was accepted.";
        }
        catch (NearbyConnectionTimeoutException ex)
        {
            LogAcceptFailed(_logger, Device.Id, ex);

            FailureReason = "The request expired before it was accepted.";
        }
        catch (NearbyException ex)
        {
            LogAcceptFailed(_logger, Device.Id, ex);

            FailureReason = "Could not accept. The device withdrew or moved out of range.";
        }
    }

    [RelayCommand]
    async Task Decline(CancellationToken cancellationToken)
    {
        if (_requestFor(Device.Id) is not { } request)
        {
            // Declining a request that already went away reaches the outcome the user asked for.
            return;
        }

        try
        {
            await request.RejectAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The user navigated away
        }
        catch (NearbyException ex)
        {
            // Nothing shown: declining a request that already went away reaches the outcome the
            // user asked for. The row leaves the collection either way.
            LogDeclineFailed(_logger, Device.Id, ex);
        }
    }

    protected override void OnDeviceChanged()
    {
        OnPropertyChanged(nameof(HasFailed));

        // A fresh request from the same device clears a stale reason: the row shows what is
        // happening now, not what failed last time.
        if (Device.Status is NearbyDeviceStatus.RequestReceived)
        {
            FailureReason = null;
        }
    }

    [LoggerMessage(
        EventId = 3,
        EventName = nameof(LogAcceptFailed),
        Level = LogLevel.Error,
        Message = "Accepting the request from {DeviceId} failed.")]
    static partial void LogAcceptFailed(ILogger logger, string deviceId, Exception exception);

    [LoggerMessage(
        EventId = 4,
        EventName = nameof(LogDeclineFailed),
        Level = LogLevel.Warning,
        Message = "Declining the request from {DeviceId} failed.")]
    static partial void LogDeclineFailed(ILogger logger, string deviceId, Exception exception);
}

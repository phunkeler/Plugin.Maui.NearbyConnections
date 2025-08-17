namespace Plugin.Maui.NearbyConnections.Advertise;

/// <inheritdoc/>
public partial class Advertiser : IAdvertiser
{
    bool _isAdvertising;

    /// <inheritdoc />
    public event EventHandler<AdvertisingStateChangedEventArgs>? AdvertisingStateChanged;

    /// <inheritdoc />
    public bool IsAdvertising
    {
        get => _isAdvertising;
        private set
        {
            if (_isAdvertising != value)
            {
                _isAdvertising = value;
                AdvertisingStateChanged?.Invoke(this, new AdvertisingStateChangedEventArgs
                {
                    IsAdvertising = value
                });
            }
        }
    }

    /// <inheritdoc />
    public async Task StartAdvertisingAsync(AdvertisingOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!IsAdvertising)
        {
            await PlatformStartAdvertising(options, cancellationToken);
            IsAdvertising = true;
        }
    }

    /// <inheritdoc />
    public async Task StopAdvertisingAsync(CancellationToken cancellationToken = default)
    {
        if (IsAdvertising)
        {
            await PlatformStopAdvertising(cancellationToken);
            IsAdvertising = false;
        }
    }
}
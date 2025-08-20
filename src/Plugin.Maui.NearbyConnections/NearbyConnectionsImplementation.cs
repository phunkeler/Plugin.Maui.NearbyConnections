using Plugin.Maui.NearbyConnections.Advertise;
using Plugin.Maui.NearbyConnections.Discover;
using Plugin.Maui.NearbyConnections.Events;
using Plugin.Maui.NearbyConnections.Models;
using AdvertisingOptions = Plugin.Maui.NearbyConnections.Advertise.AdvertisingOptions;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Implementation of the <see cref="INearbyConnections"/> interface.
/// </summary>
public partial class NearbyConnectionsImplementation : INearbyConnections, IDisposable
{
    readonly IAdvertiserFactory _advertiserFactory;
    readonly IDiscovererFactory _discovererFactory;

    IAdvertiser? _advertiser;
    IDiscoverer? _discoverer;

    /// <summary>
    /// Fired when the advertising state changes.
    /// This event remains active across advertiser dispose/recreate cycles.
    /// </summary>
    public event EventHandler<AdvertisingStateChangedEventArgs>? AdvertisingStateChanged;

    /// <summary>
    /// Fired when the discovering state changes.
    /// This event remains active across discoverer dispose/recreate cycles.
    /// </summary>
    public event EventHandler<DiscoveringStateChangedEventArgs>? DiscoveringStateChanged;

    /// <summary>
    /// Fired when a peer is discovered during the discovery process.
    /// </summary>
    public event EventHandler<PeerDiscoveredEventArgs>? PeerDiscovered;

    /// <summary>
    /// Fired when a peer's connection state changes.
    /// </summary>
    public event EventHandler<PeerConnectionChangedEventArgs>? PeerConnectionChanged;

    /// <summary>
    /// Fired when a message is received from a connected peer.
    /// </summary>
    public event EventHandler<PeerMessageReceivedEventArgs>? MessageReceived;

    /// <summary>
    /// Initializes a new instance of the <see cref="NearbyConnectionsImplementation"/> class.
    /// </summary>
    /// <param name="advertiserFactory">
    /// The factory to create <see cref="IAdvertiser"/> instances.
    /// </param>
    /// <param name="discovererFactory">
    /// The factory to create <see cref="IDiscoverer"/> instances.
    /// </param>
    public NearbyConnectionsImplementation(
        IAdvertiserFactory advertiserFactory,
        IDiscovererFactory discovererFactory)
    {
        ArgumentNullException.ThrowIfNull(advertiserFactory);
        ArgumentNullException.ThrowIfNull(discovererFactory);

        _advertiserFactory = advertiserFactory;
        _discovererFactory = discovererFactory;

        InitializePlatform();
    }

    /// <inheritdoc/>
    public async Task StartAdvertisingAsync(AdvertisingOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        await StopAdvertisingAsync(cancellationToken);

        _advertiser = _advertiserFactory.CreateAdvertiser();
        _advertiser.AdvertisingStateChanged += OnAdvertiserStateChanged;

        await _advertiser.StartAdvertisingAsync(options, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task StopAdvertisingAsync(CancellationToken cancellationToken = default)
    {
        if (_advertiser?.IsAdvertising == true)
        {
            await _advertiser.StopAdvertisingAsync(cancellationToken);
            _advertiser.AdvertisingStateChanged -= OnAdvertiserStateChanged;
            _advertiser.Dispose();
            _advertiser = null;
        }
    }

    /// <inheritdoc/>
    public async Task StartDiscoveryAsync(DiscoveringOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        await StopDiscoveryAsync(cancellationToken);

        _discoverer = _discovererFactory.CreateDiscoverer();
        _discoverer.DiscoveringStateChanged += OnDiscovererStateChanged;

        await _discoverer.StartDiscoveringAsync(options, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task StopDiscoveryAsync(CancellationToken cancellationToken = default)
    {
        if (_discoverer?.IsDiscovering ?? false)
        {
            await _discoverer.StopDiscoveringAsync(cancellationToken);
            _discoverer.DiscoveringStateChanged -= OnDiscovererStateChanged;
            _discoverer.Dispose();
            _discoverer = null;
        }
    }

    /// <summary>
    /// Disposes of resources used by the implementation.
    /// </summary>
    public void Dispose()
    {
        if (_advertiser != null)
        {
            _advertiser.AdvertisingStateChanged -= OnAdvertiserStateChanged;
            _advertiser.Dispose();
            _advertiser = null;
        }

        if (_discoverer != null)
        {
            _discoverer.DiscoveringStateChanged -= OnDiscovererStateChanged;
            _discoverer.Dispose();
            _discoverer = null;
        }

        DisposePlatform();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Forwards advertiser state changes to consumers of the INearbyConnections interface.
    /// </summary>
    /// <param name="sender">The advertiser that fired the event</param>
    /// <param name="e">The event arguments</param>
    private void OnAdvertiserStateChanged(object? sender, AdvertisingStateChangedEventArgs e)
        => AdvertisingStateChanged?.Invoke(this, e);

    /// <summary>
    /// Forwards discoverer state changes to consumers of the INearbyConnections interface.
    /// </summary>
    /// <param name="sender">The discoverer that fired the event</param>
    /// <param name="e">The event arguments</param>
    private void OnDiscovererStateChanged(object? sender, DiscoveringStateChangedEventArgs e)
        => DiscoveringStateChanged?.Invoke(this, e);

    /// <inheritdoc/>
    public Task ConnectToPeerAsync(string peerId, CancellationToken cancellationToken = default)
    {
        return ConnectToPeerAsyncImpl(peerId, cancellationToken);
    }

    /// <inheritdoc/>
    public Task DisconnectFromPeerAsync(string peerId, CancellationToken cancellationToken = default)
    {
        return DisconnectFromPeerAsyncImpl(peerId, cancellationToken);
    }

    /// <inheritdoc/>
    public Task SendMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var data = System.Text.Encoding.UTF8.GetBytes(message);
        return SendDataAsync(data, cancellationToken);
    }

    /// <inheritdoc/>
    public Task SendDataAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        return SendDataAsyncImpl(data, cancellationToken);
    }

    /// <inheritdoc/>
    public IReadOnlyList<PeerDevice> GetConnectedPeers()
    {
        return GetConnectedPeersImpl();
    }

    /// <inheritdoc/>
    public IReadOnlyList<PeerDevice> GetDiscoveredPeers()
    {
        return GetDiscoveredPeersImpl();
    }

    /// <summary>
    /// Platform-specific implementation for connecting to a peer.
    /// </summary>
    private partial Task ConnectToPeerAsyncImpl(string peerId, CancellationToken cancellationToken);

    /// <summary>
    /// Platform-specific implementation for disconnecting from a peer.
    /// </summary>
    private partial Task DisconnectFromPeerAsyncImpl(string peerId, CancellationToken cancellationToken);

    /// <summary>
    /// Platform-specific implementation for sending data.
    /// </summary>
    private partial Task SendDataAsyncImpl(byte[] data, CancellationToken cancellationToken);

    /// <summary>
    /// Platform-specific implementation for getting connected peers.
    /// </summary>
    private partial IReadOnlyList<PeerDevice> GetConnectedPeersImpl();

    /// <summary>
    /// Platform-specific implementation for getting discovered peers.
    /// </summary>
    private partial IReadOnlyList<PeerDevice> GetDiscoveredPeersImpl();

    /// <summary>
    /// Platform-specific initialization logic.
    /// </summary>
    partial void InitializePlatform();

    /// <summary>
    /// Platform-specific disposal logic.
    /// </summary>
    partial void DisposePlatform();

    /// <summary>
    ///
    /// </summary>
    /// <param name="payload"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public Task SendDataAsync(DataPayload payload, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    /// <summary>
    ///
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="fileName"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public Task SendFileAsync(string filePath, string? fileName = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();


    /// <summary>
    ///
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="streamName"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public Task SendStreamAsync(Stream stream, string streamName, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    void OnPeerDiscovered(object? sender, PeerDiscoveredEventArgs e)
        => PeerDiscovered?.Invoke(this, e);

    void OnPeerConnectionChanged(object? sender, PeerConnectionChangedEventArgs e)
        => PeerConnectionChanged?.Invoke(this, e);

    void OnMessageReceived(object? sender, PeerMessageReceivedEventArgs e)
        => MessageReceived?.Invoke(this, e);
}
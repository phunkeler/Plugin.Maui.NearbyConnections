# Plugin.Maui.NearbyConnections

A .NET MAUI plugin for peer-to-peer (P2P) connectivity with nearby devices — discover, connect, and exchange data — by unifying Google's [Nearby Connections](https://developers.google.com/nearby/connections/overview) and Apple's [Multipeer Connectivity](https://developer.apple.com/documentation/multipeerconnectivity).

[![NuGet Version](https://img.shields.io/nuget/v/Plugin.Maui.NearbyConnections)](https://www.nuget.org/packages/Plugin.Maui.NearbyConnections)
[![GitHub License](https://img.shields.io/github/license/phunkeler/Plugin.Maui.NearbyConnections)](https://github.com/phunkeler/Plugin.Maui.NearbyConnections/blob/main/LICENSE)

Peer-to-peer communication happens in two phases: **finding peers** (advertise/discover nearby devices) and **talking to them** (send/receive payloads over an established connection).

# Supported Platforms

| Platform | Minimum Version |
| --- | --- |
| Android | API 24 (_Android 7.0_) |
| iOS | iOS 13.0 |

# How connections work

Neither platform gives you direct control over which radio carries your data — both automatically negotiate between Bluetooth and Wi-Fi per connection, so you don't manage radios directly.

- **Android** ([Nearby Connections](https://developers.google.com/nearby/connections/overview)) picks between Bluetooth Classic, BLE, and Wi-Fi based on the [`Strategy`](https://developers.google.com/nearby/connections/strategies) you configure in `NearbyConnectionsOptions`: `P2pCluster` (default) allows many-to-many mesh connections at the cost of lower bandwidth, `P2pStar` allows one-to-many with higher bandwidth, and `P2pPointToPoint` is one-to-one at the highest throughput. Choose based on your topology and data size — `P2pCluster` for small messages across a cluster of devices, `P2pPointToPoint` for large file transfers between two devices.
- **iOS** (Multipeer Connectivity) auto-selects Bluetooth vs. peer-to-peer Wi-Fi vs. infrastructure Wi-Fi per link with no app-level topology control — there is no iOS equivalent to `Strategy`.

# Dependencies

Package versions are managed centrally in
[`Directory.Packages.props`](Directory.Packages.props); the plugin's own references are declared in
[`Plugin.Maui.NearbyConnections.csproj`](src/Plugin.Maui.NearbyConnections/Plugin.Maui.NearbyConnections.csproj).

# Installation
`Plugin.Maui.NearbyConnections` is available on [nuget.org](https://www.nuget.org/packages/Plugin.Maui.NearbyConnections)

```bash
dotnet add package Plugin.Maui.NearbyConnections
```

# Getting Started

## 1. Register the plugin

```csharp
// MauiProgram.cs
public static MauiApp CreateMauiApp()
{
    var builder = MauiApp.CreateBuilder();
    builder.UseMauiApp<App>();

    builder.UseNearbyConnections(opts =>
        {
#if IOS
            opts.ServiceId = "yourserviceid";
#endif
        })
        .AddAdvertiser()   // optional — registers INearbyAdvertiser (see "Higher-level services")
        .AddDiscoverer();  // optional — registers INearbyDiscoverer (see "Higher-level services")

    return builder.Build();
}
```

`UseNearbyConnections()` registers the core `INearbyConnections` API. The chained `.AddAdvertiser()` / `.AddDiscoverer()` calls are opt-in and register the higher-level `INearbyAdvertiser` / `INearbyDiscoverer` services — see [Higher-level services](#higher-level-services) below. Omit them if you only use the core API.

## 2. Platform configuration

### Android

Add to `AndroidManifest.xml`:

```xml
<uses-permission android:name="android.permission.BLUETOOTH" android:maxSdkVersion="30"/>
<uses-permission android:name="android.permission.BLUETOOTH_ADMIN" android:maxSdkVersion="30"/>
<uses-permission android:name="android.permission.BLUETOOTH_ADVERTISE" />
<uses-permission android:name="android.permission.BLUETOOTH_CONNECT" />
<uses-permission android:name="android.permission.BLUETOOTH_SCAN" android:usesPermissionFlags="neverForLocation" />
<uses-permission android:name="android.permission.ACCESS_WIFI_STATE" />
<uses-permission android:name="android.permission.CHANGE_WIFI_STATE" />
<uses-permission android:name="android.permission.ACCESS_COARSE_LOCATION" android:maxSdkVersion="32"/>
<uses-permission android:name="android.permission.ACCESS_FINE_LOCATION" android:maxSdkVersion="32"/>
<uses-permission android:name="android.permission.NEARBY_WIFI_DEVICES" android:usesPermissionFlags="neverForLocation" />
```

#### Android runtime permissions

Declaring the manifest entries above is not enough on its own — several of them are *dangerous* permissions that Android only grants after an explicit runtime request: `BLUETOOTH_ADVERTISE`, `BLUETOOTH_CONNECT`, and `BLUETOOTH_SCAN` (API 31+), `NEARBY_WIFI_DEVICES` (API 33+), and `ACCESS_FINE_LOCATION` / `ACCESS_COARSE_LOCATION` (below API 33). Request them before starting advertising or discovery:

```csharp
public async Task<bool> EnsureNearbyPermissionsAsync()
{
    if (OperatingSystem.IsAndroidVersionAtLeast(33))
    {
        var bluetooth = await Permissions.RequestAsync<Permissions.Bluetooth>();
        var nearbyWifiDevices = await Permissions.RequestAsync<Permissions.NearbyWifiDevices>();
        return bluetooth is PermissionStatus.Granted && nearbyWifiDevices is PermissionStatus.Granted;
    }

    var bluetoothStatus = await Permissions.RequestAsync<Permissions.Bluetooth>();
    var locationStatus = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
    return bluetoothStatus is PermissionStatus.Granted && locationStatus is PermissionStatus.Granted;
}
```

`Permissions.Bluetooth` covers the three `BLUETOOTH_*` runtime permissions; `Permissions.NearbyWifiDevices` covers `NEARBY_WIFI_DEVICES` on API 33+, and `Permissions.LocationWhenInUse` covers the location permissions required below API 33. See [`samples/NearbyChat/Platforms/Android/NearbyPermissions.cs`](samples/NearbyChat/Platforms/Android/NearbyPermissions.cs) for this pattern in the sample app.

On iOS there is no equivalent code step — the sample app performs no runtime permission request there; the OS itself shows the local-network prompt (backed by `NSLocalNetworkUsageDescription`) the first time the app uses the local network.

### iOS

Add to `Info.plist`:

```xml
<key>NSBonjourServices</key>
<array>
  <string>_yourserviceid._tcp</string>
  <string>_yourserviceid._udp</string>
</array>
<key>NSLocalNetworkUsageDescription</key>
<string>Used to discover and connect to nearby devices.</string>
```

`NearbyConnectionsOptions.ServiceId` is a **separate, shorter value** from the `NSBonjourServices` entries above — on iOS it's passed directly as `MCNearbyServiceAdvertiser`/`MCNearbyServiceBrowser`'s `serviceType`, which Apple requires to be a bare string 1–15 characters long (e.g. `"nearbychat"`), not the `_name._tcp` Bonjour form. There is no default; it must be set explicitly or startup validation throws.

## 3. Advertise and discover

One device advertises while the other discovers, or both do both simultaneously.

**Phase 1 — Finding peers.** One device advertises its presence; another scans for advertisers. This is a continuous `IAsyncEnumerable` stream of "device appeared" / "device disappeared" events.

**Phase 2 — Talking to them.** Once you pick a peer and connect (or accept their inbound request), a `NearbyConnection` is established. That object is itself an async stream of incoming payloads.

There are two ways to consume the plugin:

- **The core `INearbyConnections` API** exposes the two phases directly as `AdvertiseAsync` / `DiscoverAsync` streams — the minimal complete path, shown in the walkthrough below. It is also the seam to mock or implement when testing your own app code against the plugin.
- **The opt-in `INearbyAdvertiser` / `INearbyDiscoverer` services** wrap the core API with lifecycle management (`StartAsync` / `StopAsync`), a multi-subscriber event stream, and a handler-dispatch pattern — see [Higher-level services](#higher-level-services) below. The `samples/NearbyChat` app uses this path.

### Advertiser side — accept inbound connection requests

```csharp
using var cts = new CancellationTokenSource();

await foreach (var request in nearbyConnections.AdvertiseAsync(cts.Token))
{
    Console.WriteLine($"Connection request from: {request.RemoteDevice.DisplayName}");

    // Accept to get an established NearbyConnection
    NearbyConnection connection = await request.AcceptAsync(cts.Token);
    Console.WriteLine($"Connected to {connection.RemoteDevice.DisplayName}");

    // Send and receive on this connection (see section 4)
}
```

To reject a request call `request.RejectAsync()` instead of `AcceptAsync()`.

### Discoverer side — find devices and initiate a connection

```csharp
using var cts = new CancellationTokenSource();

await foreach (var evt in nearbyConnections.DiscoverAsync(cts.Token))
{
    if (evt.Type == NearbyDeviceEventType.Found)
    {
        Console.WriteLine($"Found: {evt.Device.DisplayName}");

        NearbyConnection connection = await nearbyConnections.ConnectAsync(evt.Device, cts.Token);
        Console.WriteLine($"Connected to {connection.RemoteDevice.DisplayName}");

        // Send and receive on this connection (see section 4)
    }
}
```

## 4. Send and receive data

`NearbyConnection` is obtained from `AcceptAsync` (advertiser) or `ConnectAsync` (discoverer).

### Send bytes

```csharp
byte[] data = Encoding.UTF8.GetBytes("Hello!");
await connection.SendAsync(data, cancellationToken);
```

### Send a file

```csharp
// Pass a file:// URI, or a content:// URI on Android
await connection.SendAsync("file:///path/to/file.bin", cancellationToken: cancellationToken);
```

### Track send progress

```csharp
var progress = new Progress<NearbyTransferProgress>(p =>
    Console.WriteLine($"Sent {p.BytesTransferred}/{p.TotalBytes} ({p.Fraction:P0})"));

await connection.SendAsync("file:///path/to/file.bin", progress, cancellationToken);
```

### Receive data

```csharp
await foreach (var payload in connection.ReceiveAsync(cancellationToken))
{
    if (payload is BytesPayload bytes)
    {
        string message = Encoding.UTF8.GetString(bytes.Data);
        Console.WriteLine($"From {connection.RemoteDevice.DisplayName}: {message}");
    }
    else if (payload is FilePayload file)
    {
        Console.WriteLine($"Received file: {file.FileResult.FullPath}");
    }
}
```

Received files are saved to `NearbyConnectionsOptions.ReceivedFilesDirectory`. The default differs per platform: on Android it is `FileSystem.CacheDirectory` (the OS may purge it to reclaim space); on iOS it is `FileSystem.AppDataDirectory` (persistent). If received files must persist on Android, set the option explicitly or move the files somewhere durable after receipt — see the [Configuration](#configuration) table.

### Error handling

All plugin-specific failures derive from `NearbyConnectionsException`:

- `NearbyAdvertisingException` / `NearbyDiscoveryException` — thrown when the platform fails to start advertising or discovery. On both platforms the exception is observed while enumerating the stream returned by `AdvertiseAsync` / `DiscoverAsync` (at the first `await` of the loop), so wrap the `await foreach` in a `try/catch`.
- `NearbyTransferTimeoutException` — thrown from a file-transfer `SendAsync` call when no transfer progress is observed for `NearbyConnectionsOptions.TransferInactivityTimeout` (default 10 seconds — see the [Configuration](#configuration) table).
- `NearbyConnectionsException` — the non-sealed base type. Catch it to handle all of the above; deriving from it in your own code is a supported extension contract (useful when faking `INearbyConnections` in tests).

## 5. Disconnect and clean up

```csharp
// Disconnect from a specific peer
await connection.DisposeAsync();

// Stop advertising or discovering by canceling the token passed to AdvertiseAsync/DiscoverAsync
cts.Cancel();
```

The plugin itself is a DI singleton that lives for the app's lifetime — apps normally never dispose it. `DisposeAsync` on `INearbyConnections` is one-way: it permanently shuts the instance down, so only call it if your app is done with nearby connectivity for good (there is no way to restart a disposed instance).

# Higher-level services

The opt-in `INearbyAdvertiser` / `INearbyDiscoverer` services wrap the core `INearbyConnections` API for app code that wants managed lifecycle and multi-subscriber events instead of driving the raw streams directly. This is the path the [`samples/NearbyChat`](samples/NearbyChat) reference app uses. They add:

- **Lifecycle management** — `StartAsync()` / `StopAsync()` own the advertise/discover loop; `IsAdvertising` / `IsDiscovering` report its state.
- **Multi-subscriber events** — `EventsAsync(CancellationToken)` is a fan-out stream: every subscriber gets its own copy of every event, and each new subscription starts by replaying current state (pending requests, visible devices, active connections) as synthetic events before going live.
- **Handler dispatch** — implement `IAdvertiserHandler` / `IDiscovererHandler` (all methods have default no-op implementations) and pump the stream into it with `RunAsync`.

Register them at startup (see [step 1](#1-register-the-plugin)), then:

```csharp
public class ChatViewModel : IAdvertiserHandler
{
    readonly INearbyAdvertiser _advertiser;

    public ChatViewModel(INearbyAdvertiser advertiser)
        => _advertiser = advertiser;

    // e.g. when your page appears
    public async Task OnAppearingAsync(CancellationToken token)
    {
        await _advertiser.StartAsync();

        // Dispatch events to the On* methods below until the token is canceled
        _ = _advertiser.EventsAsync(token).RunAsync(this);
    }

    // e.g. when your page disappears
    public Task OnDisappearingAsync()
        => _advertiser.StopAsync();

    async Task IAdvertiserHandler.OnConnectionRequested(AdvertiserEvent.ConnectionRequested ev)
        => await _advertiser.AcceptAsync(ev.Request);

    Task IAdvertiserHandler.OnPayloadReceived(AdvertiserEvent.PayloadReceived ev)
    {
        if (ev.Payload is BytesPayload bytes)
        {
            Console.WriteLine($"From {ev.Connection.RemoteDevice.DisplayName}: {Encoding.UTF8.GetString(bytes.Data)}");
        }

        return Task.CompletedTask;
    }
}
```

The discoverer side is mirrored: implement `IDiscovererHandler`, consume `INearbyDiscoverer.EventsAsync`, and call `INearbyDiscoverer.ConnectAsync` from `OnDeviceFound`.

Handler methods are invoked on a background thread by default; implement the handler's `Dispatcher` property to marshal them to the UI thread (the sample's ViewModels do exactly that).

Lifecycle notes:

- Canceling the token passed to `EventsAsync` detaches that subscriber only — advertising/discovery keeps running until `StopAsync`.
- A subscription survives `StartAsync`/`StopAsync` cycles on the same service instance, so a page can subscribe once and toggle advertising freely.
- Like the core API, the services are app-lifetime DI singletons — `DisposeAsync` permanently completes all subscriber streams and is normally never called by app code.

# Configuration

All `NearbyConnectionsOptions` values are read once at startup — set them in the `UseNearbyConnections(...)` (or `AddNearbyConnections(...)`) configure delegate shown in [step 1](#1-register-the-plugin). Changing them after startup has no effect.

| Member | Platform | Default | Description |
| --- | --- | --- | --- |
| `DisplayName` | Both | `DeviceInfo.Name` | The name shown to other devices when advertising/discovering. |
| `ServiceId` | Both | Android: `AppInfo.Name`; iOS: none — **must be set** | Identifier that advertisers and discoverers match on. On iOS it is the `serviceType` (bare string, 1–15 chars — see [step 2](#2-platform-configuration)); startup validation throws if unset or invalid. |
| `AutoAcceptConnections` | Both | `false` | When `true`, the platform accepts every inbound connection request automatically — the accept/reject flow in [step 3](#3-advertise-and-discover) never runs. Only enable this if you trust every peer that may discover you. |
| `ReceivedFilesDirectory` | Both | Android: `FileSystem.CacheDirectory` (OS-purgeable); iOS: `FileSystem.AppDataDirectory` (persistent) | Directory where received files are saved (see [step 4](#4-send-and-receive-data)). |
| `TransferInactivityTimeout` | Both | 10 seconds | Maximum time without a transfer progress update before an outgoing file send is aborted with `NearbyTransferTimeoutException`. Set to `Timeout.InfiniteTimeSpan` to disable. |
| `AllowSynchronousContinuations` | Both | `false` | Advanced: lets stream continuations run synchronously on the SDK's callback thread instead of hopping to the thread pool. Only enable if your consumer loop bodies are trivially fast. |
| `Strategy` | Android | `Strategy.P2pCluster` | Connection topology/bandwidth strategy (see [How connections work](#how-connections-work)). Must match between the advertising and discovering devices. |
| `UseLowPower` | Android | `false` | When `true`, only low-power mediums (like BLE) are used for advertising and discovery. |
| `ConnectionType` | Android | `ConnectionType.Balanced` | Google Nearby Connections connection type (trade-off between bandwidth and disruption to other connections). |
| `EncryptionPreference` | iOS | `MCEncryptionPreference.Required` | Encryption preference for the underlying `MCSession`. |
| `InvitationTimeout` | iOS | 30 seconds | How long `ConnectAsync` waits for the nearby advertiser to respond to the connection invitation. |

Two members change the walkthrough's behavior directly: `AutoAcceptConnections` short-circuits the accept flow in step 3, and `TransferInactivityTimeout` aborts the file sends in step 4 after a 10-second stall by default.

# Acknowledgements

-   https://github.com/jfversluis/Plugin.Maui.Feature
-   https://github.com/puguhsudarma/expo-nearby-connections
-   https://github.com/VNAPNIC/flutter_nearby_connections

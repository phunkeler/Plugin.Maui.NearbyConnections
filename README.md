# Plugin.Maui.NearbyConnections

A .NET MAUI plugin for peer-to-peer (P2P) connectivity with nearby devices — discover, connect, and exchange data — by unifying Google's [Nearby Connections](https://developers.google.com/nearby/connections/overview) and Apple's [Multipeer Connectivity](https://developer.apple.com/documentation/multipeerconnectivity).

[![NuGet Version](https://img.shields.io/nuget/v/Plugin.Maui.NearbyConnections)](https://www.nuget.org/packages/Plugin.Maui.NearbyConnections)
[![GitHub License](https://img.shields.io/github/license/phunkeler/Plugin.Maui.NearbyConnections)](https://github.com/phunkeler/Plugin.Maui.NearbyConnections/blob/main/LICENSE)

Peer-to-peer communication happens in two phases: **finding peers** (advertise/discover nearby devices) and **talking to them** (send/receive payloads over an established connection).

# What this is for

This plugin targets **foreground, both-devices-present interactions** — the two people are looking at their phones doing the thing together. It is built for:

- **File and media transfer** — send photos, documents, or data to the device next to you
- **Pairing and handoff** — device setup, account linking, transferring a session between devices
- **Bounded local exchange** — sharing between crew devices in the field, point-of-sale handoff, local data sync with no internet

**It is not built for connections that survive backgrounding.** When an app is backgrounded on iOS, the connection ends — and the plugin reports that honestly rather than pretending otherwise. This is a platform constraint, not a plugin limitation:

- **iOS.** Multipeer Connectivity has no background mode. Apple's Developer Technical Support is explicit that operating in the background is unsupported ([forum 11964](https://developer.apple.com/forums/thread/11964)); a normal app is suspended within seconds of backgrounding and the session dies silently. The plugin therefore tears the session down on `DidEnterBackground` and raises `ConnectionDropped`, so your app is told rather than left holding a dead connection.
- **Android.** No framework prohibition, but the connection dies with the process, and Doze independently suspends networking. Surviving backgrounding requires a foreground service, which is app-level work the plugin does not impose on you.

**There is no auto-reconnect, by design.** Neither platform offers a reconnect primitive — recovery means advertising and inviting again — and retry policy (how often, how long, whether to prompt) is app-specific. The plugin gives you `ConnectionDropped` and the device state to re-initiate from; your app decides whether and when.

If you need a long-lived connection that survives backgrounding, this is not the right library, and on iOS no library can give you that with Multipeer Connectivity.

# Supported Platforms

| Platform | Minimum Version |
| --- | --- |
| Android | API 24 (_Android 7.0_) |
| iOS | iOS 13.0 |

# How connections work

Neither platform gives you direct control over which radio carries your data — both automatically negotiate between Bluetooth and Wi-Fi per connection, so you don't manage radios directly.

- **Android** ([Nearby Connections](https://developers.google.com/nearby/connections/overview)) picks between Bluetooth Classic, BLE, and Wi-Fi based on the [topology](https://developers.google.com/nearby/connections/strategies) you configure via `NearbyConnectionsOptions.Topology`: `Cluster` (default) allows many-to-many mesh connections at the cost of lower bandwidth, `Star` allows one-to-many with higher bandwidth, and `PointToPoint` is one-to-one at the highest throughput. Choose based on your topology and data size — `Cluster` for small messages across a cluster of devices, `PointToPoint` for large file transfers between two devices.
- **iOS** (Multipeer Connectivity) auto-selects Bluetooth vs. peer-to-peer Wi-Fi vs. infrastructure Wi-Fi per link with no app-level topology control — there is no iOS equivalent to `Topology`.

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
    });

    return builder.Build();
}
```

`UseNearbyConnections()` registers `INearbySession` as a singleton — one radio, one native session. Inject it wherever you need nearby connectivity.

Nothing starts on its own: advertising and discovery begin only when you call them, so permission prompts happen when your app decides.

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

One device advertises while the other discovers, or both do both simultaneously. The two are independent — starting or stopping one never affects the other.

```csharp
await session.StartAdvertisingAsync();   // let others find me
await session.StartDiscoveringAsync();   // find others
```

**Device state.** Every device the session knows about lives in `session.Devices`, from first discovery until it goes out of range. Devices do not move between collections as they connect: `NearbyDevice.Status` changes instead, and the device raises `PropertyChanged`, so a bound row updates in place.

```csharp
Visible → RequestReceived → Connecting → Connected
```

`Devices` implements `INotifyCollectionChanged`, so you can bind it straight to a `CollectionView`. To show only connected devices, filter on `Status`:

```csharp
var connected = session.Devices.Where(d => d.Status is NearbyDeviceStatus.Connected);
```

### Accept inbound connection requests

```csharp
session.ConnectionRequested += async (sender, e) =>
{
    Console.WriteLine($"Connection request from: {e.Device.DisplayName}");

    // Accept to establish the connection, or RejectAsync to decline.
    NearbyConnection connection = await session.AcceptAsync(e.Device);
};
```

### Find devices and initiate a connection

Discovered devices appear in `session.Devices` with `Status == Visible`:

```csharp
NearbyConnection connection = await session.ConnectAsync(device);
```

`ConnectAsync` completes when the remote device accepts. If it rejects, or the device goes away, the call throws and the device returns to `Visible` — it never gets stuck mid-handshake.

### Know when a connection opens or closes

```csharp
session.ConnectionEstablished += (sender, e) => StartConsuming(e.Connection);
session.ConnectionDropped     += (sender, e) => Cleanup(e.Device);
```

> **Handlers run on the UI thread and run synchronously.** The session marshals every collection change, property change, and event to the dispatcher for you, so bindings are safe without extra work — but keep handlers fast and do no I/O in them. Inbound payloads are a stream precisely so that consuming them can be asynchronous (see section 4).

> **Unsubscribe what you subscribe.** The session is a singleton that outlives your pages. A page that does `+=` without a matching `-=` stays alive for the life of the app, and revisiting it attaches a second handler — after five visits every event fires five times. See `BasePageViewModel.RegisterSessionSubscription` in [`samples/NearbyChat`](samples/NearbyChat) for the pattern. Payload loops need no such care: they end by themselves when the connection drops.

> **Subscribe before the first connection exists.** These events do not replay. Whatever subscribes to `ConnectionEstablished` to start consuming payloads must already be constructed by the time a connection opens, or it never starts a loop for that connection and the peer's messages silently never arrive. Registering it as a DI singleton is *not* sufficient — singletons are constructed on first resolution, so a consumer resolved only by a page opened after connecting is built too late. Register it as an `IMauiInitializeService`, which MAUI constructs during `Build()`. See [`docs/PAYLOAD-DELIVERY.md`](docs/PAYLOAD-DELIVERY.md#your-consumer-must-be-constructed-before-the-first-connection).

## 4. Send and receive data

`NearbyConnection` is obtained from `AcceptAsync`, from `ConnectAsync`, or from `NearbyDevice.Connection` while the device is connected.

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

Payloads are a stream, not an event: the loop body is the seam where your own async work goes, and it is awaited before the next payload is taken.

```csharp
await foreach (var payload in connection.ReceiveAsync())
{
    if (payload is BytesPayload bytes)
    {
        string message = Encoding.UTF8.GetString(bytes.Data);
        Console.WriteLine($"From {connection.RemoteDevice.DisplayName}: {message}");
    }
    else if (payload is FilePayload file)
    {
        Console.WriteLine($"Received file: {file.FileResult.FullPath}");
        await GenerateThumbnailAsync(file.FileResult.FullPath);   // awaited in-loop
    }
}
```

**One consumer per connection.** The stream can only be enumerated once. If several parts of your app need inbound data, consume it in one place and fan out from there (the sample publishes a domain message via `IMessenger`).

**Do not pass `DisconnectedToken` to `ReceiveAsync`.** It is unnecessary — the loop already ends by itself on disconnect — and harmful: cancellation is observed on every iteration, so an already-cancelled token discards payloads that arrived just before the peer went away.

Received files are saved to `NearbyConnectionsOptions.ReceivedFilesDirectory`. The default differs per platform: on Android it is `FileSystem.CacheDirectory` (the OS may purge it to reclaim space); on iOS it is `FileSystem.AppDataDirectory` (persistent). If received files must persist on Android, set the option explicitly or move the files somewhere durable after receipt — see the [Configuration](#configuration) table.

### Error handling

All plugin-specific failures derive from `NearbyConnectionsException`:

- `NearbyAdvertisingException` / `NearbyDiscoveryException` — the platform failed to start advertising or discovery, most often because permissions were denied or the radio is off.
- `NearbyConnectionTimeoutException` — thrown from `ConnectAsync` when the remote device does not answer within `NearbyConnectionsOptions.InvitationTimeout` (default 30 seconds), typically because it moved out of range mid-handshake or nobody answered the prompt. The device returns to `Visible`, so retrying is reasonable.
- `NearbyTransferTimeoutException` — thrown from a file-transfer `SendAsync` call when no transfer progress is observed for `NearbyConnectionsOptions.TransferInactivityTimeout` (default 10 seconds — see the [Configuration](#configuration) table).
- `NearbyConnectionsException` — the non-sealed base type. Catch it to handle all of the above; deriving from it in your own code is a supported extension contract (useful when faking `INearbySession` in tests).

## 5. Disconnect and clean up

```csharp
// Disconnect from one peer, leaving every other connection intact
await session.DisconnectAsync(device);

// Stop one activity without affecting the other
await session.StopAdvertisingAsync();
await session.StopDiscoveringAsync();

// Stop everything and disconnect every peer
await session.StopAsync();
```

`StopAsync()` is the consumer-facing teardown: it stops advertising and discovery and disconnects everything, but leaves the session usable — start again whenever you like. The session itself is a DI singleton owned by the container; app code never disposes it, so no single page can shut down connectivity for the whole app.

# Configuration

All `NearbyConnectionsOptions` values are read once at startup — set them in the `UseNearbyConnections(...)` (or `AddNearbyConnections(...)`) configure delegate shown in [step 1](#1-register-the-plugin). Changing them after startup has no effect.

| Member | Platform | Default | Description |
| --- | --- | --- | --- |
| `DisplayName` | Both | `DeviceInfo.Name` | The name shown to other devices when advertising/discovering. |
| `ServiceId` | Both | Android: `AppInfo.Name`; iOS: none — **must be set** | Identifier that advertisers and discoverers match on. On iOS it is the `serviceType` (bare string, 1–15 chars — see [step 2](#2-platform-configuration)); startup validation throws if unset or invalid. |
| `ReceivedFilesDirectory` | Both | Android: `FileSystem.CacheDirectory` (OS-purgeable); iOS: `FileSystem.AppDataDirectory` (persistent) | Directory where received files are saved (see [step 4](#4-send-and-receive-data)). |
| `TransferInactivityTimeout` | Both | 10 seconds | Maximum time without a transfer progress update before an outgoing file send is aborted with `NearbyTransferTimeoutException`. Set to `Timeout.InfiniteTimeSpan` to disable. |
| `AllowSynchronousContinuations` | Both | `false` | Advanced: lets stream continuations run synchronously on the SDK's callback thread instead of hopping to the thread pool. Only enable if your consumer loop bodies are trivially fast. |
| `Topology` | Android | `NearbyTopology.Cluster` | How devices may connect — `Cluster` (many-to-many), `Star` (one-to-many), or `PointToPoint` (one-to-one, highest bandwidth). See [How connections work](#how-connections-work). Must match between the advertising and discovering devices. |
| `UseLowPower` | Android | `false` | When `true`, only low-power mediums (like BLE) are used for advertising and discovery. |
| `ConnectionType` | Android | `NearbyConnectionType.Balanced` | How aggressively a connection may use the radio — `Balanced`, `HighBandwidth`, or `NonDisruptive` (trade-off between throughput and disruption to other connections). |
| `EncryptionPreference` | iOS | `NearbyEncryptionPreference.Required` | Whether the link must be encrypted. Android always encrypts and ignores this. |
| `InvitationTimeout` | **Both** | 30 seconds | How long `ConnectAsync` waits for the remote device to answer before throwing `NearbyConnectionTimeoutException`. Set to `Timeout.InfiniteTimeSpan` to wait indefinitely. |

One member changes the walkthrough's behavior directly: `TransferInactivityTimeout` aborts the file sends in step 4 after a 10-second stall by default.

# Acknowledgements

-   https://github.com/jfversluis/Plugin.Maui.Feature
-   https://github.com/puguhsudarma/expo-nearby-connections
-   https://github.com/VNAPNIC/flutter_nearby_connections

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

| Dependency | Android | iOS |
| --- | :---: | :---: |
| [Microsoft.Extensions.DependencyInjection.Abstractions]() | ✅ | ✅ |
| [Microsoft.Maui.Core](https://www.nuget.org/packages/Microsoft.Maui.Core) | ✅  | ✅ |
| [Xamarin.GooglePlayServices.Nearby](https://www.nuget.org/packages/Xamarin.GooglePlayServices.Nearby/) | ✅ | |

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
    => MauiApp.CreateBuilder()
        .UseMauiApp<App>()
        .AddNearbyConnections()
        .Build();
```

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

The tier-1 API (`INearbyConnections`) exposes these two phases directly as `AdvertiseAsync` / `DiscoverAsync` streams, as shown below. For MAUI app code, the tier-2 services (`INearbyAdvertiser` / `INearbyDiscoverer`) provide additional utilities.

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

Received files are saved to `NearbyConnectionsOptions.ReceivedFilesDirectory` (default: `FileSystem.AppDataDirectory`).

## 5. Disconnect and clean up

```csharp
// Disconnect from a specific peer
await connection.DisposeAsync();

// Stop advertising or discovering by canceling the token passed to AdvertiseAsync/DiscoverAsync
cts.Cancel();

// Dispose the plugin when done (e.g. in page OnDisappearing)
await nearbyConnections.DisposeAsync();
```

# Acknowledgements

-   https://github.com/jfversluis/Plugin.Maui.Feature
-   https://github.com/puguhsudarma/expo-nearby-connections
-   https://github.com/VNAPNIC/flutter_nearby_connections

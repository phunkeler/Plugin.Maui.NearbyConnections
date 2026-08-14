# Plugin.Maui.NearbyConnections

A .NET MAUI plugin for peer-to-peer (P2P) connectivity with nearby devices: discover, connect, and
exchange data. It unifies Google's [Nearby Connections](https://developers.google.com/nearby/connections/overview)
and Apple's [Multipeer Connectivity](https://developer.apple.com/documentation/multipeerconnectivity).

[![NuGet Version](https://img.shields.io/nuget/v/Plugin.Maui.NearbyConnections)](https://www.nuget.org/packages/Plugin.Maui.NearbyConnections)
[![GitHub License](https://img.shields.io/github/license/phunkeler/Plugin.Maui.NearbyConnections)](https://github.com/phunkeler/Plugin.Maui.NearbyConnections/blob/main/LICENSE)

Peer-to-peer communication happens in two phases: finding peers (advertise and discover nearby
devices), then talking to them (send and receive payloads over an established connection).

# What this is for

This plugin targets foreground, both-devices-present interactions. Two people looking at their
phones, doing the thing together:

- **File and media transfer** — send photos, documents, or data to the device next to you
- **Pairing and handoff** — device setup, account linking, moving a session between devices
- **Bounded local exchange** — sharing between crew devices in the field, point-of-sale handoff,
  local data sync with no internet

## What it is not for

**Connections do not survive backgrounding.**

**iOS.** Multipeer Connectivity has no background mode. Apple's Developer Technical Support states
that operating in the background is unsupported ([forum 11964](https://developer.apple.com/forums/thread/11964)).
A backgrounded app is suspended within seconds and the session dies silently. The plugin tears it
down on `DidEnterBackground` and reports every device back to `Visible` through `Devices.Changes`,
rather than leaving you holding a dead connection.

**Android.** No framework prohibition, but the connection dies with the process, and Doze
independently suspends networking. Surviving backgrounding requires a foreground service. That is
app-level work the plugin does not impose on you.

**There is no auto-reconnect.** Neither platform offers a reconnect primitive; recovery means
advertising and inviting again, and retry policy is app-specific. The plugin gives you device state
to re-initiate from. Your app decides whether and when.

If you need a connection that survives backgrounding, this is the wrong library. On iOS, no library
can give you that with Multipeer Connectivity.

# Supported platforms

| Platform | Minimum version |
| --- | --- |
| Android | API 24 (_Android 7.0_) |
| iOS | iOS 13.0 |

# Installation

`Plugin.Maui.NearbyConnections` is available on [nuget.org](https://www.nuget.org/packages/Plugin.Maui.NearbyConnections).

```bash
dotnet add package Plugin.Maui.NearbyConnections
```

# Getting started

## 1. Register the plugin

```csharp
// MauiProgram.cs
public static MauiApp CreateMauiApp()
{
    var builder = MauiApp.CreateBuilder();
    builder.UseMauiApp<App>();

    builder.UseNearby(opts =>
    {
#if IOS
        opts.ServiceId = "yourserviceid";
#endif
    });

    return builder.Build();
}
```

`UseNearby()` registers `INearby` as a singleton: one radio, one native session. Inject it wherever
you need nearby connectivity.

Nothing starts on its own. Advertising and discovery begin only when you call them, so permission
prompts happen when your app decides.

## 2. Platform configuration

### Android

**No manifest changes are required.** The package declares every permission Nearby Connections
needs, and they merge into your app's manifest automatically.

**Recommended:** if your app does not derive the user's physical location from Bluetooth or Wi-Fi
scan results, add this to `Platforms/Android/AndroidManifest.xml`. The package cannot declare the
`neverForLocation` flag itself, and without it Android treats these two permissions as implying
location access:

```xml
<uses-permission
  android:name="android.permission.BLUETOOTH_SCAN"
  android:usesPermissionFlags="neverForLocation" />
<uses-permission
  android:name="android.permission.NEARBY_WIFI_DEVICES"
  android:usesPermissionFlags="neverForLocation" />
```

#### Permissions the package declares

| Permission | Notes |
| --- | --- |
| `INTERNET`, `ACCESS_NETWORK_STATE` | Install-time |
| `ACCESS_WIFI_STATE`, `CHANGE_WIFI_STATE` | Install-time |
| `BLUETOOTH`, `BLUETOOTH_ADMIN` | Capped at `maxSdkVersion="30"` |
| `ACCESS_COARSE_LOCATION`, `ACCESS_FINE_LOCATION` | Capped at `maxSdkVersion="32"` |
| `BLUETOOTH_ADVERTISE`, `BLUETOOTH_CONNECT`, `BLUETOOTH_SCAN` | Runtime, API 31+ |
| `NEARBY_WIFI_DEVICES` | Runtime, API 33+ |

Media permissions are **not** declared. Reading a file you choose to send is your app's concern.

To override any of them, redeclare the permission in your own `AndroidManifest.xml`; your version
wins. Two caveats:

- **Restate `maxSdkVersion` when you redeclare.** Redeclaring `BLUETOOTH` without the cap widens it
  from `maxSdkVersion="30"` to every API level.
- **`tools:node="remove"` does not work** against these. The directive is copied into the final
  manifest verbatim and the permission is still requested.

#### Android runtime permissions

The manifest entries above are not enough. Several are *dangerous* permissions that Android grants
only after an explicit runtime request: `BLUETOOTH_ADVERTISE`, `BLUETOOTH_CONNECT`, and
`BLUETOOTH_SCAN` (API 31+), `NEARBY_WIFI_DEVICES` (API 33+), and `ACCESS_FINE_LOCATION` /
`ACCESS_COARSE_LOCATION` (below API 33). Request them before starting advertising or discovery:

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

`Permissions.Bluetooth` covers the three `BLUETOOTH_*` runtime permissions,
`Permissions.NearbyWifiDevices` covers `NEARBY_WIFI_DEVICES` on API 33+, and
`Permissions.LocationWhenInUse` covers the location permissions required below API 33. See
[`samples/NearbyChat/Platforms/Android/NearbyPermissions.cs`](samples/NearbyChat/Platforms/Android/NearbyPermissions.cs)
for this pattern in the sample app.

iOS needs no equivalent code. The OS shows the local-network prompt (backed by
`NSLocalNetworkUsageDescription`) the first time the app uses the local network.

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

`NearbyOptions.ServiceId` is a **separate, shorter value** from the `NSBonjourServices` entries
above. On iOS it is passed directly as the `serviceType` for `MCNearbyServiceAdvertiser` and
`MCNearbyServiceBrowser`, which Apple requires to be a bare string 1–15 characters long (for
example `"nearbychat"`), not the `_name._tcp` Bonjour form. There is no default; set it explicitly
or startup validation throws.

## 3. Advertise and discover

One device advertises while the other discovers, or both do both simultaneously. The two are
independent: starting or stopping one never affects the other.

```csharp
await nearby.StartAdvertisingAsync();   // let others find me
await nearby.StartDiscoveryAsync();     // find others
```

**Device state.** Every device the plugin knows about lives in `nearby.Devices`, from first
discovery until it goes out of range. Devices do not move between collections as they connect.
`NearbyDevice.Status` changes instead, and the device raises `PropertyChanged`, so a bound row
updates in place.

```csharp
Visible → RequestReceived → Connecting → Connected
```

Any rejection, timeout, or disconnect returns a device to `Visible` rather than to a separate
failure state. See the [full lifecycle diagram](docs/DEVICE-LIFECYCLE.md#consumer-facing-summary)
for every transition, including the iOS caveat that `Connecting` is optional.

`Devices` implements `INotifyCollectionChanged`, so you can bind it straight to a `CollectionView`.
To show only connected devices, filter on `Status`:

```csharp
var connected = nearby.Devices.Where(d => d.Status is NearbyDeviceStatus.Connected);
```

### Accept inbound connection requests

A device asking to connect appears with `Status == RequestReceived`:

```csharp
await foreach (var change in nearby.Devices.Changes.WithCancellation(cancellationToken))
{
    if (change.Device.Status is NearbyDeviceStatus.RequestReceived)
    {
        // Accept to establish the connection, or RejectAsync to decline.
        NearbyConnection connection = await nearby.AcceptAsync(change.Device);
    }
}
```

### Find devices and initiate a connection

Discovered devices appear in `nearby.Devices` with `Status == Visible`:

```csharp
NearbyConnection connection = await nearby.ConnectAsync(device);
```

`ConnectAsync` completes when the remote device accepts. If it rejects, or the device goes away,
the call throws and the device returns to `Visible`. It never gets stuck mid-handshake.

### Know when a connection opens or closes

Every lifecycle transition arrives on one stream, as a change to the device's `Status`:

```csharp
await foreach (var change in nearby.Devices.Changes.WithCancellation(cancellationToken))
{
    var device = change.Device;

    if (change.Action is not NearbyDeviceChangeAction.Removed
        && device.Status is NearbyDeviceStatus.Connected
        && nearby.TryGetConnection(device.Id, out var connection))
    {
        StartConsuming(connection);
    }
}
```

Three things to know about this loop:

**Changes do not arrive on the UI thread.** `INearby` has no UI thread affinity and marshals
nothing for you. Platform callbacks are drained by an internal pump, so changes reach your loop on
a thread-pool thread, never the dispatcher. Marshal in the loop body with
`await Dispatcher.DispatchAsync(...)`, or bind to a `NearbyDeviceCollection`, which does it for
you:

```csharp
// ItemsSource="{Binding Devices}"
public NearbyDeviceCollection Devices { get; } = new(nearby, Dispatcher.Dispatch);
```

Because the loop body is `async`, you can await inside it. An event handler could not.

**Ending the loop is the only cleanup.** There is nothing to unsubscribe from. Cancel the token, or
`break`, and the watcher is gone. A page that watches with its navigation token cannot leak the way
an undetached `+=` handler could.

> **Start watching before the first connection exists.** `Changes` does not replay. A consumer that
> starts a receive loop must already be running by the time a connection opens, or it never starts
> one for that connection and the peer's messages silently never arrive. Registering it as a DI
> singleton is *not* sufficient, because singletons are constructed on first resolution, so a
> consumer resolved only by a page opened after connecting is built too late. Register it as an
> `IMauiInitializeService`, which MAUI constructs during `Build()`. A late starter can recover the
> current state by reading `nearby.Devices` before it begins watching. See
> [`docs/PAYLOAD-DELIVERY.md`](docs/PAYLOAD-DELIVERY.md#your-consumer-must-be-constructed-before-the-first-connection).

## 4. Send and receive data

Get a `NearbyConnection` from `AcceptAsync`, from `ConnectAsync`, or by looking one up with
`nearby.TryGetConnection(device.Id, out var connection)` while the device is connected.

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

Payloads are a stream, not an event. The loop body is the seam where your own async work goes, and
it is awaited before the next payload is taken.

```csharp
await foreach (var payload in connection.ReceiveAsync())
{
    if (payload is NearbyBytesPayload bytes)
    {
        string message = Encoding.UTF8.GetString(bytes.Data);
        Console.WriteLine($"From {connection.RemoteDevice.DisplayName}: {message}");
    }
    else if (payload is NearbyFilePayload file)
    {
        Console.WriteLine($"Received file: {file.FileResult.FullPath}");
        await GenerateThumbnailAsync(file.FileResult.FullPath);   // awaited in-loop
    }
}
```

**One consumer per connection.** The stream can only be enumerated once. If several parts of your
app need inbound data, consume it in one place and fan out from there. The sample publishes a
domain message via `IMessenger`.

**Do not pass `DisconnectedToken` to `ReceiveAsync`.** It is unnecessary, because the loop already
ends by itself on disconnect. It is also harmful: cancellation is observed on every iteration, so
an already-cancelled token discards payloads that arrived just before the peer went away.

Received files are saved to `NearbyOptions.ReceivedFilesDirectory`. The default differs per
platform. On Android it is `FileSystem.CacheDirectory`, which the OS may purge to reclaim space; on
iOS it is `FileSystem.AppDataDirectory`, which persists. If received files must persist on Android,
set the option explicitly, or move the files somewhere durable after receipt. See the
[Configuration](#configuration) table.

### Error handling

All plugin-specific failures derive from `NearbyException`:

- `NearbyAdvertisingException` / `NearbyDiscoveryException` — the platform failed to start
  advertising or discovery, most often because permissions were denied or the radio is off.
- `NearbyConnectionTimeoutException` — thrown from `ConnectAsync` when the remote device does not
  answer within `NearbyOptions.InvitationTimeout` (default 30 seconds), typically because it moved
  out of range mid-handshake or nobody answered the prompt. The device returns to `Visible`, so
  retrying is reasonable.
- `NearbyTransferTimeoutException` — thrown from a file-transfer `SendAsync` call when no transfer
  progress is observed for `NearbyOptions.TransferInactivityTimeout` (default 10 seconds; see the
  [Configuration](#configuration) table).
- `NearbyException` — the non-sealed base type. Catch it to handle all of the above. Deriving from
  it in your own code is a supported extension contract, useful when faking `INearby` in tests.

## 5. Disconnect and clean up

```csharp
// Disconnect from one peer, leaving every other connection intact
await nearby.DisconnectAsync(device);

// Stop one activity without affecting the other
await nearby.StopAdvertisingAsync();
await nearby.StopDiscoveryAsync();

// Stop everything and disconnect every peer
await nearby.StopAsync();
```

`StopAsync()` is the consumer-facing teardown. It stops advertising and discovery and disconnects
everything, but leaves the plugin usable, so you can start again whenever you like. `INearby` is a
DI singleton owned by the container. App code never disposes it, so no single page can shut down
connectivity for the whole app.

# How connections work

Neither platform gives you direct control over which radio carries your data. Both negotiate
between Bluetooth and Wi-Fi automatically, per connection.

**Android** ([Nearby Connections](https://developers.google.com/nearby/connections/overview)) picks
between Bluetooth Classic, BLE, and Wi-Fi based on the
[topology](https://developers.google.com/nearby/connections/strategies) you set via
`NearbyOptions.Android.Topology`. `Cluster` (the default) allows many-to-many mesh at lower
bandwidth, `Star` allows one-to-many at higher bandwidth, and `PointToPoint` is one-to-one at the
highest throughput. Use `Cluster` for small messages across a group of devices, `PointToPoint` for
large file transfers between two.

**iOS** (Multipeer Connectivity) auto-selects Bluetooth, peer-to-peer Wi-Fi, or infrastructure
Wi-Fi per link, with no app-level topology control. There is no iOS equivalent to `Topology`.

# Configuration

All `NearbyOptions` values are read once at startup. Set them in the `UseNearby(...)` or
`AddNearby(...)` configure delegate shown in [step 1](#1-register-the-plugin). Changing them after
startup has no effect.

| Member | Platform | Default | Description |
| --- | --- | --- | --- |
| `DisplayName` | Both | `DeviceInfo.Name` | The name shown to other devices when advertising or discovering. |
| `ServiceId` | Both | Android: `AppInfo.Name`; iOS: none, **must be set** | Identifier that advertisers and discoverers match on. On iOS it is the `serviceType` (bare string, 1–15 chars; see [step 2](#2-platform-configuration)). Startup validation throws if unset or invalid. |
| `ReceivedFilesDirectory` | Both | Android: `FileSystem.CacheDirectory` (OS-purgeable); iOS: `FileSystem.AppDataDirectory` (persistent) | Directory where received files are saved. See [step 4](#4-send-and-receive-data). |
| `TransferInactivityTimeout` | Both | 10 seconds | Maximum time without a transfer progress update before an outgoing file send is aborted with `NearbyTransferTimeoutException`. Set to `Timeout.InfiniteTimeSpan` to disable. |
| `AllowSynchronousContinuations` | Both | `false` | Advanced. Lets **payload** stream continuations run synchronously on the writer's thread instead of hopping to the thread pool. Does not affect `Devices.Changes`, which always schedules to the thread pool. Only enable if your consumer loop bodies are trivially fast. |
| `Topology` | Android | `NearbyTopology.Cluster` | How devices may connect: `Cluster` (many-to-many), `Star` (one-to-many), or `PointToPoint` (one-to-one, highest bandwidth). See [How connections work](#how-connections-work). Must match between the advertising and discovering devices. |
| `UseLowPower` | Android | `false` | When `true`, only low-power mediums such as BLE are used for advertising and discovery. |
| `ConnectionType` | Android | `NearbyConnectionType.Balanced` | How aggressively a connection may use the radio: `Balanced`, `HighBandwidth`, or `NonDisruptive`. Trades throughput against disruption to other connections. |
| `EncryptionPreference` | iOS | `NearbyEncryptionPreference.Required` | Whether the link must be encrypted. Android always encrypts and ignores this. |
| `InvitationTimeout` | Both | 30 seconds | How long `ConnectAsync` waits for the remote device to answer before throwing `NearbyConnectionTimeoutException`. Set to `Timeout.InfiniteTimeSpan` to wait indefinitely. |

`TransferInactivityTimeout` is the one that shows up in the walkthrough directly. By default it
aborts file sends in step 4 after a 10-second stall.

# Logging

The plugin logs through `Microsoft.Extensions.Logging`, using whatever providers your app has
already configured. It installs no provider of its own and sends nothing off the device.

On a healthy session it is silent at default levels. Routine events (discovery, connections,
payloads) are `Debug` and `Trace`, so the framework's default `Information` threshold filters them
out. What you see by default is `Warning` and `Error`, plus one `Information` message: the iOS
background teardown.

To troubleshoot, turn the library up:

```csharp
builder.Logging.AddFilter("Plugin.Maui.NearbyConnections", LogLevel.Debug);
```

`Debug` is the right level for devices not appearing or connections not forming. `Trace` adds one
entry per payload, for transfer problems.

Device display names appear in messages at `Debug`. They are user-chosen and often personal. See
[`docs/LOGGING.md`](docs/LOGGING.md) for the full level contract, per-category filters, EventIDs
worth alerting on, and privacy guidance.

# Dependencies

Package versions are managed centrally in
[`Directory.Packages.props`](Directory.Packages.props). The plugin's own references are declared in
[`Plugin.Maui.NearbyConnections.csproj`](src/Plugin.Maui.NearbyConnections/Plugin.Maui.NearbyConnections.csproj).

# Acknowledgements

-   https://github.com/jfversluis/Plugin.Maui.Feature
-   https://github.com/puguhsudarma/expo-nearby-connections
-   https://github.com/VNAPNIC/flutter_nearby_connections

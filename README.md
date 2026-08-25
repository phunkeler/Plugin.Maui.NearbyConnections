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
rather than leaving you holding a dead connection. The teardown also stops advertising and
discovery, which surfaces as `false` on `AdvertisingChanges` and `DiscoveryChanges`.

**Android.** No framework prohibition, but the connection dies with the process, and Doze
independently suspends networking. Surviving backgrounding requires a foreground service. That is
app-level work the plugin does not impose on you.

**There is no auto-reconnect.** Neither platform offers a reconnect primitive; recovery means
advertising and inviting again, and retry policy is app-specific. The plugin gives you device state
to re-initiate from. Your app decides whether and when.

If you need a connection that survives backgrounding, this is the wrong library. On iOS, no library
can give you that with Multipeer Connectivity.

# Supported platforms

Requires **.NET 10** and .NET MAUI 10. The package ships `net10.0-android`, `net10.0-ios`, and a
`net10.0` reference target; there is no .NET 8 or .NET 9 build, so installing into an earlier
project fails to restore with NU1202.

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
        opts.ServiceId = "yourserviceid";
    });

    return builder.Build();
}
```

`UseNearby()` registers `INearby` as a singleton: one radio, one native session. Inject it wherever
you need nearby connectivity.

Nothing starts on its own. Advertising and discovery begin only when you call them, so permission
prompts happen when your app decides.

`UseNearby()` needs logging registered, which `MauiApp.CreateBuilder()` already does. It also
registers `TimeProvider.System` if your app has not registered a `TimeProvider` of its own.

### Consume every connection, from anywhere, at any time

`nearby.Connections` is a broadcast stream: each enumeration first yields every connection still
open, then each connection as it opens. Starting late loses nothing — a connection opened before
your consumer even existed is replayed to it, with its unread payloads still buffered.

```csharp
// Anywhere, any time — no initializer ritual, nothing missed by starting late.
await foreach (var connection in nearby.Connections.WithCancellation(appToken))
{
    _ = ConsumeAsync(connection);
}

async Task ConsumeAsync(NearbyConnection connection)
{
    await foreach (var payload in connection.ReceiveAsync())
    {
        Handle(payload);
    }
}
```

`samples/NearbyChat/Services/NearbyIngestionService.cs` is the complete version — an ordinary DI
singleton whose constructor starts the loop.

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
[`samples/NearbyChat/Platforms/Android/NearbyPermissions.cs`](https://github.com/phunkeler/Plugin.Maui.NearbyConnections/blob/main/samples/NearbyChat/Platforms/Android/NearbyPermissions.cs)
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

**Both stop on their own.** `nearby.IsAdvertising` and `nearby.IsDiscovering` report the current
state, and the platform changes it without asking: backgrounding tears the session down, and a
radio fault ends a scan mid-session. Read the property for the current value, then watch the
matching stream for what happens next — the same state-plus-deltas shape as `Devices` and
`Devices.Changes`.

```csharp
// each item is the flag's new value; apply it rather than re-reading the property
await foreach (var isAdvertising in nearby.AdvertisingChanges.WithCancellation(cancellationToken))
{
    headerLabel.Text = isAdvertising ? "Broadcasting..." : "Not broadcasting";
}
```

**Device state.** Every device the plugin knows about lives in `nearby.Devices`, from first
discovery until it goes out of range. Devices do not move between collections as they connect.
`NearbyDevice.Status` changes instead. A `NearbyDevice` is an immutable snapshot rather than a
bindable object, so a status change arrives as an `Updated` entry on `Devices.Changes` carrying a
new snapshot — see [step 3](#know-when-a-connection-opens-or-closes).

```text
Visible → RequestReceived → Connecting → Connected
```

Any rejection, timeout, or disconnect returns a device to `Visible` rather than to a separate
failure state. See the [full lifecycle diagram](https://github.com/phunkeler/Plugin.Maui.NearbyConnections/blob/main/docs/DEVICE-LIFECYCLE.md#consumer-facing-summary)
for every transition, including the iOS caveat that `Connecting` is optional.

`nearby.Devices` is a read-only snapshot list, **not** an observable collection: it raises no
change notification, so binding it directly to a `CollectionView` renders once and never updates.
To bind, construct a [`NearbyDeviceCollection<TRow>`](#know-when-a-connection-opens-or-closes), which
watches `Devices.Changes` for you and does raise `INotifyCollectionChanged`. To show only connected
devices, filter on `Status`:

```csharp
var connected = nearby.Devices.Where(d => d.Status is NearbyDeviceStatus.Connected);
```

### Accept inbound connection requests

Inbound requests arrive on `nearby.Requests` — a broadcast stream that replays the requests still
outstanding, then follows live arrivals. The accept and reject decision lives on the request:

```csharp
await foreach (var request in nearby.Requests.WithCancellation(pageToken))
{
    if (await ConfirmWithUserAsync(request.RemoteDevice))
    {
        NearbyConnection connection = await request.AcceptAsync();
    }
    else
    {
        await request.RejectAsync();
    }
}
```

**Requests expire.** If nobody answers within `NearbyOptions.InboundRequestTimeout` (default 30
seconds), the library rejects the request and the device returns to `Visible`. The request's
`Expired` task completes — await it to dismiss a prompt — and a late `AcceptAsync` or
`RejectAsync` throws `NearbyRequestExpiredException`, so handle that if your UI can be slow to
respond.

The expiry exists because neither platform withdraws a stale request, and on iOS the *asking* device
gives up on its own schedule. Without it, a prompt can outlive the attempt behind it and accepting
would connect to nothing.

`NearbyDevice.RequestExpiresAt` carries the deadline, so you can show a countdown:

```csharp
if (device.RequestExpiresAt is { } expiresAt)
{
    var remaining = expiresAt - DateTimeOffset.UtcNow;
}
```

It is a deadline rather than a remaining duration because `NearbyDevice` is an immutable snapshot —
a stored duration would be stale the moment you read it. Drive the countdown from your own UI timer.
The value is `null` when a request does not expire, and once the device leaves `RequestReceived`.

### Find devices and initiate a connection

Discovered devices appear in `nearby.Devices` with `Status == Visible`:

```csharp
NearbyConnection connection = await nearby.ConnectAsync(device);
```

`ConnectAsync` completes when the remote device accepts. If it rejects, or the device goes away,
the call throws and the device returns to `Visible`. It never gets stuck mid-handshake.

### Know when a connection opens or closes

An opened connection arrives on `nearby.Connections` (above). A closed one completes its own
`Disconnected` task, carrying why it ended:

```csharp
NearbyEndReason reason = await connection.Disconnected;
```

Every lifecycle transition also arrives on `nearby.Devices.Changes` as a change to the device's
`Status`, with `NearbyDeviceChange.Reason` carrying the locally-observed reason where one exists.
That stream is for state — binding, dashboards, presence — while `Connections` and `Requests`
deliver the things your code must handle.

Three things to know about the `Devices.Changes` loop:

**Changes do not arrive on the UI thread.** `INearby` has no UI thread affinity and marshals
nothing for you. Platform callbacks are drained by an internal pump, so changes reach your loop on
a thread-pool thread, never the dispatcher. Marshal in the loop body with
`await Dispatcher.DispatchAsync(...)`, or bind to a `NearbyDeviceCollection<TRow>`, which does it
for you. To bind devices straight from XAML, project each one onto itself:

```csharp
// ItemsSource="{Binding Devices}"
// IDispatcher.Dispatch returns bool, so wrap it rather than passing it as a method group.
public NearbyDeviceCollection<NearbyDevice> Devices { get; }
    = new(nearby,
          marshal: action => dispatcher.Dispatch(action),
          project: static device => device);
```

To bind rows that carry their own commands or state, project onto a row type instead. Pass
`update` so a row is reused across its device's status changes rather than rebuilt:

```csharp
public NearbyDeviceCollection<DeviceRow> Rows { get; }
    = new(nearby,
          marshal: action => dispatcher.Dispatch(action),
          project: device => new DeviceRow(device, nearby),
          filter: device => device.Status is NearbyDeviceStatus.Visible,
          update: (row, device) => row.Update(device));
```

Because the loop body is `async`, you can await inside it. An event handler could not.

**Ending the loop is the only cleanup.** There is nothing to unsubscribe from. Cancel the token, or
`break`, and the watcher is gone. A page that watches with its navigation token cannot leak the way
an undetached `+=` handler could.

> **`Changes` does not replay — and does not need to.** The snapshot is the catch-up: read
> `nearby.Devices` for the current state, then watch `Changes` for what happens next. Deliverables
> are different: `nearby.Requests` and `nearby.Connections` replay what is still outstanding, so a
> payload consumer or a request prompt that starts late misses nothing that still matters.

## 4. Send and receive data

Get a `NearbyConnection` from the `nearby.Connections` stream, from `request.AcceptAsync`, from
`ConnectAsync`, or by looking one up with `nearby.TryGetConnection(device.Id, out var connection)`
while the device is connected — all of them hand you the same instance.

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
        // Move it to keep it. The move consumes the staged copy, so nothing is left behind.
        var kept = file.MoveTo(Path.Combine(FileSystem.AppDataDirectory, file.FileResult.FileName));
        await GenerateThumbnailAsync(kept.FullPath);   // awaited in-loop
    }
}
```

**One consumer per connection.** The stream can only be enumerated once. If several parts of your
app need inbound data, consume it in one place and fan out from there. The sample publishes a
domain message via `IMessenger`.

**Do not pass `DisconnectedToken` to `ReceiveAsync`.** It is unnecessary, because the loop already
ends by itself on disconnect. It is also harmful: cancellation is observed on every iteration, so
an already-cancelled token discards payloads that arrived just before the peer went away.

#### Received files are yours to keep or discard

A received file is staged in app-private storage that the operating system may purge, and it
belongs to your app from the moment the payload reaches your loop.

- **To keep it:** call `MoveTo`. Inside the app sandbox this is a rename, so it returns
  immediately. Use the `FileResult` it returns — the payload's own still points at the staging
  path, which no longer exists.
- **To discard it:** do nothing. Files you do not move are deleted when the session is disposed,
  and the operating system may reclaim them before that.

The behaviour is identical on Android and iOS.

`FileName` and `ContentType` come from the sending device. Treat both as untrusted input, and
validate the content before acting on the declared type.

The library deliberately does not cap the size of an inbound file, filter files by type, or encrypt
what it stages. Watch `NearbyConnection.InboundProgress` and disconnect if a transfer is larger than
your app accepts, inspect the file after it arrives, and rely on platform storage encryption
(Android File-Based Encryption, iOS Data Protection) for data at rest.

### Error handling

All plugin-specific failures derive from `NearbyException`:

- `NearbyAdvertisingException` / `NearbyDiscoveryException` — the platform failed to start
  advertising or discovery, most often because permissions were denied or the radio is off.
- `NearbyConnectionTimeoutException` — thrown when no connection is established in time: from
  `ConnectAsync` after `NearbyOptions.ConnectTimeout` (default 30 seconds), or from `AcceptAsync`
  after `NearbyOptions.AcceptTimeout` (default 15 seconds). Usually the device moved out of range
  mid-handshake, or nobody answered the prompt. The device returns to `Visible`, so retrying is
  reasonable.
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

`DisconnectAsync()` and `StopAsync()` both wait for in-flight inbound work on the connections they
tear down — on Android, an inbound file payload that is still copying — before returning. The wait
is bounded to a few seconds per connection; a wedged copy is abandoned rather than left to hang
disposal, and the app is logged. With several connections open, `StopAsync()` tears them down in
turn, so the total wait scales with connection count.

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
| `TransferInactivityTimeout` | Both | 10 seconds | Maximum time without a transfer progress update before an outgoing file send is aborted with `NearbyTransferTimeoutException`. Set to `Timeout.InfiniteTimeSpan` to disable. |
| `DiscoveryRefreshInterval` | Both | 30 seconds | How often discovery restarts to re-check what is in range. Devices a new pass does not re-report are removed; a connected or mid-handshake device is never removed. Set to `null` to never restart, and drive `StopDiscoveryAsync`/`StartDiscoveryAsync` yourself. |
| `AutoAcceptConnectionRequests` | Both | `false` | When `true`, every inbound request is accepted as it arrives and `RequestReceived` is never observed. **This accepts any device that knows the service identifier** — neither platform authenticates the remote device, so enable it only for a kiosk, paired appliance, or trusted network. |
| `ConnectTimeout` | Both | 30 seconds | How long `ConnectAsync` waits before throwing `NearbyConnectionTimeoutException`. Covers the remote user deciding, so it is the more generous of the two. Set to `Timeout.InfiniteTimeSpan` to wait indefinitely. |
| `AcceptTimeout` | Both | 15 seconds | How long `AcceptAsync` waits before throwing `NearbyConnectionTimeoutException`. Shorter by default because the decision is already made and only the handshake remains. Set to `Timeout.InfiniteTimeSpan` to wait indefinitely. |
| `InboundRequestTimeout` | Both | 30 seconds | How long an unanswered inbound request stays outstanding before the library rejects it and the device returns to `Visible`. Read `NearbyDevice.RequestExpiresAt` to show a countdown. Set to `Timeout.InfiniteTimeSpan` to leave requests outstanding. |
| `Android.Topology` | Android | `NearbyTopology.Cluster` | How devices may connect: `Cluster` (many-to-many), `Star` (one-to-many), or `PointToPoint` (one-to-one, highest bandwidth). See [How connections work](#how-connections-work). Must match between the advertising and discovering devices. |
| `Android.UseLowPower` | Android | `false` | When `true`, only low-power mediums such as BLE are used for advertising and discovery. |
| `Android.ConnectionType` | Android | `NearbyConnectionType.Balanced` | How aggressively a connection may use the radio: `Balanced`, `HighBandwidth`, or `NonDisruptive`. Trades throughput against disruption to other connections. |
| `Apple.EncryptionPreference` | iOS | `NearbyEncryptionPreference.Required` | Whether the link must be encrypted. Android always encrypts and ignores this. |
| `Apple.StartFailureGraceWindow` | iOS | 250 ms | Advanced. How long `StartAdvertisingAsync`/`StartDiscoveryAsync` wait for a start failure before reporting success. Multipeer Connectivity has no start-success callback, so a failure arriving after this window surfaces as a stream fault and a log entry instead. |

`TransferInactivityTimeout` is the one that shows up in the walkthrough directly. By default it
aborts file sends in step 4 after a 10-second stall.

# Security considerations

A proximity network is an untrusted network. Any device in radio range that knows your `ServiceId`
can discover this app and request a connection. Read this section before you ship.

## What the plugin gives you

- **The link is encrypted.** Android encrypts every connection and ignores
  `Apple.EncryptionPreference`. On iOS the default is `NearbyEncryptionPreference.Required`. Do not
  lower it in a shipping app.
- **Inbound file names are safe to write.** The plugin strips any directory component a sender puts
  in a file name, so a name like `../../databases/app.db` cannot escape the staging directory.
  Colliding names get a ` (1)` suffix instead of overwriting.
- **Remote display names are cleaned before use.** Control characters are removed and the name is
  capped at 64 characters, so a peer cannot forge log records through its own name.
- **Staged files are temporary.** Inbound files land in the app cache directory and are deleted
  when the session is disposed. Move a file you want to keep with `NearbyFilePayload.MoveTo`.

## What the plugin does not give you

**Neither platform authenticates the remote device, and this plugin does not add authentication.**

- `NearbyDevice.DisplayName` is chosen by the remote device. It is not verified, it is not unique,
  and two devices can advertise the same name. Never use it as identity, and never use it to make
  an authorization decision.
- `NearbyDevice.Id` identifies a device only within the current session. It is not stable across
  sessions and is not a credential.
- A connection proves proximity and nothing else. If your app needs to know *who* is on the other
  end, authenticate at the application layer: exchange a token, a pairing code, or a signature over
  the connection after it opens, and treat the connection as untrusted until that succeeds.

`AutoAcceptConnectionRequests` accepts **every** request from **any** device that knows the service
identifier. It exists for closed environments — a kiosk, a test rig, a demo. Leave it `false` in a
shipping app and accept requests explicitly, so a person can decide.

## Treat every payload as hostile input

Payload bytes and file contents come from an unauthenticated sender. The plugin delivers them
verbatim and does not inspect them.

- Validate and size-check bytes before you parse them. Do not deserialize a payload into a type
  that can execute code or allocate without bound.
- Do not trust an inbound file's name or extension to describe its contents.
- There is no inbound size limit. A peer can send a file large enough to fill the cache directory,
  so check `NearbyTransferProgress.TotalBytes` and disconnect if a transfer is larger than your app
  expects.
- Payloads buffer in memory until something reads them. Consume `ReceiveAsync` for the life of the
  connection, or disconnect.

## Logs contain device names and file paths

Display names appear at `Debug` and file paths at `Error`. That is standard `ILogger` behaviour, not
a defect, but it means identity data reaches whatever sink your app configures. Raise the minimum
level for the `Plugin.Maui.NearbyConnections` category if that data must not be persisted. See
[Logging](#logging).

## Android permissions

The package declares the permissions Nearby Connections needs, capped with `maxSdkVersion` where a
newer permission replaces an older one. It declares no `uses-feature` entry, so it never narrows
which devices can install your app. If your app does not derive location from scan results, add
`android:usesPermissionFlags="neverForLocation"` to `BLUETOOTH_SCAN` in your own manifest — see
[Platform configuration](#2-platform-configuration).

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
[`docs/LOGGING.md`](https://github.com/phunkeler/Plugin.Maui.NearbyConnections/blob/main/docs/LOGGING.md) for the full level contract, per-category filters, EventIDs
worth alerting on, and privacy guidance.

# Dependencies

Package versions are managed centrally in
[`Directory.Packages.props`](https://github.com/phunkeler/Plugin.Maui.NearbyConnections/blob/main/Directory.Packages.props). The plugin's own references are declared in
[`Plugin.Maui.NearbyConnections.csproj`](https://github.com/phunkeler/Plugin.Maui.NearbyConnections/blob/main/src/Plugin.Maui.NearbyConnections/Plugin.Maui.NearbyConnections.csproj).

# Acknowledgements

-   https://github.com/jfversluis/Plugin.Maui.Feature
-   https://github.com/puguhsudarma/expo-nearby-connections
-   https://github.com/VNAPNIC/flutter_nearby_connections

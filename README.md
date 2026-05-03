# Plugin.Maui.NearbyConnections

A .NET MAUI plugin for peer-to-peer (P2P) connectivity with nearby devices by unifying Google's [Nearby Connections](https://developers.google.com/nearby/connections/overview) and Apple's [Multipeer Connectivity](https://developer.apple.com/documentation/multipeerconnectivity) capabilities.

[![NuGet Version](https://img.shields.io/nuget/v/Plugin.Maui.NearbyConnections)](https://www.nuget.org/packages/Plugin.Maui.NearbyConnections)
[![GitHub License](https://img.shields.io/github/license/phunkeler/Plugin.Maui.NearbyConnections)](https://github.com/phunkeler/Plugin.Maui.NearbyConnections/blob/main/LICENSE)

# Supported Platforms

| Platform | Minimum Version |
| --- | --- |
| Android | API 24 (_Android 7.0_) |
| iOS | iOS 13.0 |

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

</details>

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

The service ID in `NSBonjourServices` must match `NearbyConnectionsOptions.ServiceId` (_**default**: app name_).

## 3. Discover and connect

Devices in range must advertise and/or discover simultaneously. Typically one device advertises while another discovers, or both do both.

```csharp
// Subscribe to events before starting
nearbyConnections.DeviceFound += (s, e) =>
{
    Console.WriteLine($"Found: {e.NearbyDevice.DisplayName}");

    // Request a connection
    await nearbyConnections.RequestConnectionAsync(e.NearbyDevice);
};

nearbyConnections.DeviceStateChanged += (s, e) =>
{
    if (e.CurrentState == NearbyDeviceState.Connected)
        Console.WriteLine($"Connected to {e.NearbyDevice.DisplayName}");
};

// Start advertising so other devices can find this one
await nearbyConnections.StartAdvertisingAsync();

// Start discovering nearby advertisers
await nearbyConnections.StartDiscoveryAsync();
```

When `AutoAcceptConnections` is `false`, handle inbound requests manually:

```csharp
nearbyConnections.ConnectionRequested += async (s, e) =>
{
    bool accept = true; // your logic here
    await nearbyConnections.RespondToConnectionAsync(e.NearbyDevice, accept);
};
```

## 4. Send and receive data

### Send bytes

```csharp
var device = nearbyConnections.Devices.First(d => d.State == NearbyDeviceState.Connected);
byte[] data = Encoding.UTF8.GetBytes("Hello!");

await nearbyConnections.SendAsync(device, data);
```

### Send a file

```csharp
// Pass a file:// URI or a content:// URI (Android)
await nearbyConnections.SendAsync(device, "file:///path/to/file.bin");
```

### Track send progress

```csharp
var progress = new Progress<NearbyTransferProgress>(p =>
{
    Console.WriteLine($"Sent {p.BytesTransferred}/{p.TotalBytes} ({p.Fraction:P0})");
});

await nearbyConnections.SendAsync(device, data, progress);
```

### Receive data

```csharp
nearbyConnections.DataReceived += (s, e) =>
{
    if (e.Payload is BytesPayload bytes)
    {
        string message = Encoding.UTF8.GetString(bytes.Data);
        Console.WriteLine($"From {e.NearbyDevice.DisplayName}: {message}");
    }
    else if (e.Payload is FilePayload file)
    {
        Console.WriteLine($"Received file: {file.FileResult.FullPath}");
    }
};
```

Received files are saved to `NearbyConnectionsOptions.ReceivedFilesDirectory` (default: `FileSystem.CacheDirectory`).

## 5. Disconnect and clean up

```csharp
// Disconnect a specific peer
await nearbyConnections.DisconnectAsync(device);

// Stop advertising/discovering
await nearbyConnections.StopAdvertisingAsync();
await nearbyConnections.StopDiscoveryAsync();

// Dispose when done (e.g. page OnDisappearing)
nearbyConnections.Dispose();
```

# Acknowledgements

-   https://github.com/jfversluis/Plugin.Maui.Feature
-   https://github.com/puguhsudarma/expo-nearby-connections
-   https://github.com/VNAPNIC/flutter_nearby_connections

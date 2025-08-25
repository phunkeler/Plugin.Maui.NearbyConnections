<div align="center">
  <picture>
    <img src=".assets/nuget.svg" width="180">
  </picture>

  <h1>
    Plugin.Maui.NearbyConnections
  </h1>
  <p>
    A .NET MAUI plugin for peer-to-peer (P2P) connectivity with nearby devices by unifying Google's <a href="https://developers.google.com/nearby/connections/overview" target="_blank">Nearby Connections</a> and Apple's <a href="https://developer.apple.com/documentation/multipeerconnectivity" target="_blank">Multipeer Connectivity</a> capabilities.
  </p>
</div>
<h1>
</h1>
</br>

<div align="center">
  <div>
    <a href="https://www.nuget.org/packages/Plugin.Maui.NearbyConnections">
      <img alt="NuGet Version" src="https://img.shields.io/nuget/v/Plugin.Maui.NearbyConnections">
    </a>
  </div>
  <div>
    <a href="https://codecov.io/gh/phunkeler/Plugin.Maui.NearbyConnections">
      <img alt="Codecov Report" src="https://img.shields.io/codecov/c/gh/phunkeler/Plugin.Maui.NearbyConnections/main?">
    </a>
  </div>
  <div>
    <a href="https://github.com/phunkeler/Plugin.Maui.NearbyConnections/actions/workflows/codeql.yml">
        <img alt="CodeQL Report" src="https://github.com/phunkeler/Plugin.Maui.NearbyConnections/actions/workflows/codeql.yml/badge.svg">
    </a>
  </div>
  <div>
    <a href="https://github.com/phunkeler/Plugin.Maui.NearbyConnections/blob/main/LICENSE">
      <img alt="GitHub License" src="https://img.shields.io/github/license/phunkeler/Plugin.Maui.NearbyConnections">
    </a>
  </div>
  </p>
</div>

## Class Diagram

-   IAdvertiser
    -   StartAdvertising(AdvertisingOptions options)
    -   StopAdvertising();
    -   TODO: Define interface for registering callback(s)/responding to events
    -   Android:
        -   User-defined implementation of [ConnectionLifecycleCallback](https://developers.google.com/android/reference/com/google/android/gms/nearby/connection/ConnectionLifecycleCallback)
    -   iOS:
        -   User-defined implementation of [IMCNearbyServiceAdvertiserDelegate](https://developer.apple.com/documentation/multipeerconnectivity/mcnearbyserviceadvertiser)
-   IDiscoverer

## Features

-   Configurable

## **Unique Advertising & Discovery Sessions**

```csharp
await NearbyConnections.Current.StartAdvertisingAsync(new AdvertisingOptions
{
    DisplayName = "Name1",
    ServiceName = "service-1"
});

await NearbyConnections.Current.StopAdvertisingAsync();

// ✅ Different parameters - fully supported
await NearbyConnections.Current.StartAdvertisingAsync(new AdvertisingOptions
{
    DisplayName = "Name2",     // Changed
    ServiceName = "service-2", // Changed
    AdvertisingInfo = { ... }  // Changed
});
```

-   Send arbitrary `IDictionary<string, string>` data with advertisements (Maybe call this "AdvertisementInfo")

# Getting Started

`Plugin.Maui.NearbyConnections` is available on [nuget.org](https://www.nuget.org/packages/Plugin.Maui.NearbyConnections)

## **dotnet**

```bash
dotnet add package Plugin.Maui.NearbyConnections -s https://api.nuget.org/v3/index.json
```

</details>

# Usage

`MauiProgram.cs`:
Following setup patterns established by [Microsoft.Maui.Essentials](https://www.nuget.org/packages/Microsoft.Maui.Essentials) first need to register the Feature with the MauiAppBuilder following the same pattern that the .NET MAUI Essentials libraries follow.

# Supported Platforms

| Platform | Minimum Version Supported |
| -------- | ------------------------- |
| iOS      | Not set in project.       |
| Android  | Not set in project        |

# 🔗 Dependencies

-   This NuGet package is [x MB]](LINK_TO_PROOF)
-   In the case of the sample app (Plugin.Maui.NearbyConnections.Sample) it increased .apk/.ipa size by [X](LINK_TO_PROOF)2

    ## Android

    -   [Xamarin.GooglePlayServices.Nearby](https://www.nuget.org/packages/Xamarin.GooglePlayServices.Nearby/) (1.25 MB)
    -   Requires Google Play Services on device

    ## iOS

    -   Native `MultipeerConnectivity` framework
    -   No external dependencies

# DEBUGGING

-   adb exec-out run-as com.companyname.nearbychat cat "/data/data/com.companyname.nearbychat/files/NearbyChat.db3" > %userprofile%\Downloads\NearbyChat.db3

# Dev Todo:

-   Keep "Advertise" and "Discovery" boundaries clear but connected via I
-   Expose everything through IAdvertiser & IDiscoverer
-   "INearbyConnections" stays clean +
-   Allow consumers

# Acknowledgements

-   https://github.com/jfversluis/Plugin.Maui.Feature
-   https://github.com/puguhsudarma/expo-nearby-connections
-   https://github.com/VNAPNIC/flutter_nearby_connections

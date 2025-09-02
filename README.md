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

# Supported Platforms

| Platform | Minimum Version Supported |
| -------- | ------------------------- |
| iOS      | Not set in project.       |
| Android  | Not set in project        |
|          |                           |

# Dependencies
-   [Microsoft.Maui.Controls]()
-   [Xamarin.GooglePlayServices.Nearby](https://www.nuget.org/packages/Xamarin.GooglePlayServices.Nearby/)

# Getting Started
`Plugin.Maui.NearbyConnections` is ~~available~~ on [nuget.org](https://www.nuget.org/packages/Plugin.Maui.NearbyConnections)

## **dotnet**

```bash
dotnet add package Plugin.Maui.NearbyConnections -s https://api.nuget.org/v3/index.json
```

</details>

# Usage

## MauiProgram.cs

```csharp
public static MauiApp CreateMauiApp()
    => MauiApp.CreateBuilder()
        .UseMauiApp<App>()
        .ConfigureNearbyConnections() // Defaults
        .Build();

```

```csharp
public static MauiApp CreateMauiApp()
    => MauiApp.CreateBuilder()
        .UseMauiApp<App>()
        .ConfigureNearbyConnections(builder => // Custom Config
        {

        })
        .Build();

```

## Options

### AdvertisingOptions

These options can be set at; `MauiProgram.cs` (_simple_) or per `StartAdvertisingAsync` call (_advanced_).

-   DisplayName
-   ServiceName
-   Limited amount of Arbitrary key/value pairs

## Architecture

### Phases

#### **Pre-Connection** (_`Advertise/Discover` --> `Invite` --> `Accept/Decline`_)

During the pre-connection phase, nearby devices; make themselves known (`Start/Stop Advertising`), find out about others (`Start/Stop Discovering`), request to connect (`SendInvitation`), choose to accept/decline (`Accept/Decline Invitation`), and then submit their response (`AnswerInvitation`).

The following table shows how critical native callback/delegate methods map to plugin events.

| Plugin.Maui.NearbyConnections.Events |                                                                                                                                                                                 Android                                                                                                                                                                                 |                                                                                                                                                                        iOS                                                                                                                                                                        |
| :----------------------------------: | :---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------: | :-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------: |
|          InvitationReceived          | [OnConnectionInitiated](https://developers.google.com/android/reference/com/google/android/gms/nearby/connection/ConnectionLifecycleCallback#public-abstract-void-onconnectioninitiated-string-endpointid,-connectioninfo-connectioninfo) (_[dotnet](https://github.com/dotnet/android-libraries/blob/eb048f14d0ac1fd66144572cbca3cc476b353cb5/docs/artifact-list.md)_) | [DidReceiveInvitationFromPeer](<https://developer.apple.com/documentation/multipeerconnectivity/mcnearbyserviceadvertiserdelegate/advertiser(_:didreceiveinvitationfrompeer:withcontext:invitationhandler:)>) (_[dotnet](https://github.com/dotnet/macios/blob/0d3c2e24a0ee88420142fd6710571d1260b99c15/src/multipeerconnectivity.cs#L413-L416)_) |
|        NearbyConnectionFound         |         [OnEndpointFound](https://developers.google.com/android/reference/com/google/android/gms/nearby/connection/EndpointDiscoveryCallback#public-abstract-void-onendpointfound-string-endpointid,-discoveredendpointinfo-info) (_[dotnet](https://github.com/dotnet/android-libraries/blob/eb048f14d0ac1fd66144572cbca3cc476b353cb5/docs/artifact-list.md)_)         |                             [FoundPeer](<https://developer.apple.com/documentation/multipeerconnectivity/mcnearbyservicebrowserdelegate/browser(_:foundpeer:withdiscoveryinfo:)>) (_[dotnet](https://github.com/dotnet/macios/blob/0d3c2e24a0ee88420142fd6710571d1260b99c15/src/multipeerconnectivity.cs#L515-L525)_)                             |
|         NearbyConnectionLost         |                        [OnEndpointLost](https://developers.google.com/android/reference/com/google/android/gms/nearby/connection/EndpointDiscoveryCallback#public-abstract-void-onendpointlost-string-endpointid) (_[dotnet](https://github.com/dotnet/android-libraries/blob/eb048f14d0ac1fd66144572cbca3cc476b353cb5/docs/artifact-list.md)_)                         |                                       [LostPeer](<https://developer.apple.com/documentation/multipeerconnectivity/mcnearbyservicebrowserdelegate/browser(_:lostpeer:)>) (_[dotnet](https://github.com/dotnet/macios/blob/0d3c2e24a0ee88420142fd6710571d1260b99c15/src/multipeerconnectivity.cs#L527-L533)_)                                       |
|          InvitationAnswered          |                                                          [OnConnectionResult](https://developers.google.com/android/reference/com/google/android/gms/nearby/connection/ConnectionLifecycleCallback#public-abstract-void-onconnectionresult-string-endpointid,-connectionresolution-resolution) (_[dotnet]()_)                                                           |                                          [DidChange](<https://developer.apple.com/documentation/multipeerconnectivity/mcsessiondelegate/session(_:peer:didchange:)>) (_[dotnet](https://github.com/dotnet/macios/blob/0d3c2e24a0ee88420142fd6710571d1260b99c15/src/multipeerconnectivity.cs#L250-L257)_)                                          |

#### **Connection** (_`Data Exchange` --> `Resource Transfer` --> `State Changes`_)

During the connection phase, connected devices exchange messages (`Send/Receive Message`) and handle connection state changes (`Connected/Disconnected`).

The following table shows how critical native callback/delegate methods map to plugin events.

| Plugin.Maui.NearbyConnections.Events |                                                                                                                                                                Android                                                                                                                                                                |                                                                                                                                    iOS                                                                                                                                    |
| :----------------------------------: | :-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------: | :-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------: |
|           MessageReceived            | [OnPayloadReceived](https://developers.google.com/android/reference/com/google/android/gms/nearby/connection/PayloadCallback#public-abstract-void-onpayloadreceived-string-endpointid,-payload-payload) (_[dotnet](https://github.com/dotnet/android-libraries/blob/eb048f14d0ac1fd66144572cbca3cc476b353cb5/docs/artifact-list.md)_) | [DidReceiveData](<https://developer.apple.com/documentation/multipeerconnectivity/mcsessiondelegate/session(_:didreceive:frompeer:)>) (_[dotnet](https://github.com/dotnet/macios/blob/0d3c2e24a0ee88420142fd6710571d1260b99c15/src/multipeerconnectivity.cs#L259-L266)_) |
|           PeerDisconnected           |      [OnDisconnected](https://developers.google.com/android/reference/com/google/android/gms/nearby/connection/ConnectionLifecycleCallback#public-abstract-void-ondisconnected-string-endpointid) (_[dotnet](https://github.com/dotnet/android-libraries/blob/eb048f14d0ac1fd66144572cbca3cc476b353cb5/docs/artifact-list.md)_)       |      [DidChange](<https://developer.apple.com/documentation/multipeerconnectivity/mcsessiondelegate/session(_:peer:didchange:)>) (_[dotnet](https://github.com/dotnet/macios/blob/0d3c2e24a0ee88420142fd6710571d1260b99c15/src/multipeerconnectivity.cs#L250-L257)_)      |

#### **Post-Connection** (`Cleanup`)

### Event-Driven

-   Events
-   EventProcessors

# DEBUGGING

-   adb -s R3CR609PX6W exec-out run-as com.companyname.nearbychat cat "/data/data/com.companyname.nearbychat/files/NearbyChat.db3" > %userprofile%\Downloads\NearbyChat.db3

# Acknowledgements

-   https://github.com/jfversluis/Plugin.Maui.Feature
-   https://github.com/puguhsudarma/expo-nearby-connections
-   https://github.com/VNAPNIC/flutter_nearby_connections

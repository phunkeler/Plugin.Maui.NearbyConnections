# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a .NET MAUI plugin that provides peer-to-peer (P2P) connectivity with nearby devices by unifying Google's Nearby Connections (Android) and Apple's Multipeer Connectivity (iOS) capabilities.

## Tech Stack
- .NET 10 / C# (modern style)
- .NET MAUI (Minimal APIs)

## Build System
- **Project Type**: Multi-targeted .NET MAUI plugin
- **Target Frameworks**: `net10.0`, `net10.0-android`, `net10.0-ios` (`Directory.Build.props:3-8`)
- **Solution File**: `Plugin.Maui.NearbyConnections.slnx` (Visual Studio solution)

### Build Commands

```bash
# Build the project
dotnet build

# Pack for NuGet
dotnet pack

# Build for specific platform
dotnet build -f net10.0-android
dotnet build -f net10.0-ios
```

## Architecture

The plugin is a two-tier API over a single sealed class, `NearbyConnectionsImplementation`.

### Tier 1 — `Connections/`: the platform-partial core

`NearbyConnectionsImplementation` implements `INearbyConnections` as a partial class split by platform:

- `INearbyConnections.cs` — the full public API surface
- `NearbyConnections.shared.cs` — DI constructor; `AdvertiseAsync`/`DiscoverAsync` swap in a fresh session channel per enumeration (`Interlocked.Exchange`); `ConnectAsync` and dispose dispatch
- `NearbyConnections.android.cs` — Android advertising, discovery, and data transfer via Google Nearby Connections
- `NearbyConnections.ios.cs` — iOS advertising, discovery, and data transfer via Multipeer Connectivity
- `NearbyConnections.net.cs` — Generic .NET stub (throws `PlatformNotSupportedException`)
- `NearbyConnections.log.cs` — Source-generated `ILogger` partial methods
- `NearbyConnections.events.cs` — Event declarations and `internal On*()` raise helpers
- `NearbyConnectionsOptions.cs` / `.android.cs` / `.ios.cs` / `.net.cs` — One-time startup configuration (configure via the registration delegate; not re-read after start), one partial per platform
- `PeerRegistry.cs` — Internal generic `PeerRegistry<THandle>`, the `ConcurrentDictionary`-backed registry of remote devices and their native handles
- `LocalPeerIdentityStore.ios.cs` / `PeerIdArchive.ios.cs` / `PeerKeyProvider.ios.cs` / `PeerRegistryExtensions.ios.cs` — iOS `MCPeerID` identity persistence and peer-registry helpers
- `OutgoingTransfer.cs` — Inactivity-timeout wrapper for outgoing file transfers
- `NearbyConnection.cs` — `IAsyncDisposable` handle to an established P2P session (`SendAsync`/`ReceiveAsync`/`Disconnected`)
- `NearbyConnectionRequest.cs` — Inbound connection request with `AcceptAsync`/`RejectAsync`
- `NearbyConnectionsException.cs` — `NearbyConnectionsException` base type (supported extension contract) plus sealed `NearbyTransferTimeoutException`
- `ConnectionRole.cs` — `Initiator` / `Acceptor` enum
- `ControlMessage.cs` — Internal wire-protocol control messages

### Tier 2 — `Advertiser/` and `Discoverer/`: opt-in higher-level services

`INearbyAdvertiser`/`NearbyAdvertiser` and `INearbyDiscoverer`/`NearbyDiscoverer` wrap Tier 1 with a multi-subscriber, `IAsyncEnumerable`-based event stream (`EventsAsync(CancellationToken)` yielding `AdvertiserEvent` / `DiscovererEvent`), fanned out via the shared `ChannelBroadcaster<T>` primitive (`src/Plugin.Maui.NearbyConnections/ChannelBroadcaster.cs`). Both services compose the internal `ConnectionLifecycle<TPending, TEvent>` helper (`src/Plugin.Maui.NearbyConnections/ConnectionLifecycle.cs`) for the shared start/stop, monitoring, and event fan-out plumbing. Each has its own handler interface (`IAdvertiserHandler`, `IDiscovererHandler`), event-extension helpers, dedicated exception type (`NearbyAdvertisingException`, `NearbyDiscoveryException`), and source-generated logging partial (`NearbyAdvertiser.log.cs`, `NearbyDiscoverer.log.cs`). Tier 2 is optional — apps can consume `INearbyConnections` directly without it.

### `Options/`: DI registration

- `ServiceCollectionExtensions.cs` — `AddNearbyConnections()` registers Tier 1 as a singleton and returns a `NearbyConnectionsBuilder`; `.AddAdvertiser()` / `.AddDiscoverer()` on that builder opt in to Tier 2
- `MauiAppBuilderExtensions.cs` (project root) — `UseNearbyConnections()` is the `MauiAppBuilder`-facing entry point apps call from `MauiProgram.cs`; internally wraps `AddNearbyConnections()`
- `NearbyConnectionsBuilder.cs`, `NearbyConnectionsOptionsValidator.cs` (+ `.android.cs` / `.ios.cs` / `.net.cs`) — the opt-in builder type and per-platform startup validation via `IValidateOptions<T>`

Typical app registration (`samples/NearbyChat/MauiProgram.cs`):

```csharp
builder.UseNearbyConnections(opts =>
    {
#if IOS
        opts.ServiceId = "nearbychat";
#endif
    })
    .AddAdvertiser()
    .AddDiscoverer();
```

### Other supporting types

- `Devices/NearbyDevice.cs`, `NearbyDeviceEvent.cs`, `NearbyDeviceEventType.cs` — device record and visibility-event types; the thread-safe (`ConcurrentDictionary`-backed) device registry itself is the internal `PeerRegistry<THandle>` in `Connections/`
- `Transfer/NearbyPayload.cs` — abstract `NearbyPayload` record with `BytesPayload`/`FilePayload` subtypes; `NearbyTransferProgress.cs` for progress reporting

### Platform Dependencies

- **Android**: `Xamarin.GooglePlayServices.Nearby` package (`Directory.Packages.props`)
- **iOS**: Native `MultipeerConnectivity` framework

### Build Configuration

- **Platform Detection**: `IsTargetPlatformAndroid` and `IsTargetPlatformIos` properties (`Directory.Build.props`)
- **File Exclusion**: Platform-specific files are excluded from non-matching builds (`src/Directory.Build.targets`)
- **Global Usings**: Platform namespaces are auto-imported per target (`src/Directory.Build.targets`)

## Development Standards

- **Nullable Reference Types**: Enabled (`Directory.Build.props`)
- **Code Analysis**: Latest recommended level with warnings as errors (`Directory.Build.props`)
- **Documentation**: XML documentation required (`Directory.Build.props`)

## Coding Style

- **Always use braces `{ }`** for every `if`, `else`, `foreach`, `for`, `while`, and `do` body — even single-line bodies. No brace-free one-liners. This matches `csharp_prefer_braces = true:warning` in `.editorconfig`.
- **Omit redundant accessibility modifiers** where the default already applies (e.g. `private` on class members). This matches `dotnet_style_require_accessibility_modifiers = omit_if_default:warning` in `.editorconfig`.

## Test Coding Style

Tests must use strict **Arrange / Act / Assert** structure with a blank line separating each section and a comment marking each phase:

```csharp
// Arrange
var sut = new MyClass();

// Act
var result = sut.DoSomething();

// Assert
Assert.Equal(expected, result);
```

- Every test method must have all three `// Arrange`, `// Act`, `// Assert` comments — even if a section is trivial.
- No logic in Assert sections; compute expected values in Arrange.
- One logical assertion per test where possible; group related property checks with a single assertion object.

## Current Implementation Status

Full P2P lifecycle is implemented on Android and iOS across both tiers:
- Advertise / discover nearby devices (`INearbyConnections` directly, or `INearbyAdvertiser`/`INearbyDiscoverer`)
- Request, accept, and reject connections
- Send and receive bytes (`BytesPayload`) and files (`FilePayload`) with progress reporting
- Disconnect from peers, with `ConnectionDropped`/`DeviceDisconnected` reliably published on both real disconnects and on `Stop`

Generic .NET target throws `PlatformNotSupportedException` for all operations.

An Appium-based on-device UI test suite (`test/NearbyChat.UiTests/`) exercises the full lifecycle end-to-end (connection setup/teardown, bytes transfer, disconnects, photo/video attachments) against the `NearbyChat` sample app, running in CI via `.github/workflows/ui-tests.yml`.

## iOS Configuration Requirements

Apps using this plugin need these `Info.plist` entries:

```xml
<key>NSBonjourServices</key>
<array>
  <string>_yourserviceid._tcp</string>
  <string>_yourserviceid._udp</string>
</array>
<key>NSLocalNetworkUsageDescription</key>
<string>Used to discover and connect to nearby devices.</string>
```

`NearbyConnectionsOptions.ServiceId` is a **separate, shorter value** from the `Info.plist` entries above — it is passed directly as `MCNearbyServiceAdvertiser`/`MCNearbyServiceBrowser`'s `serviceType`, which Apple requires to be a bare string 1–15 characters long (e.g. `"nearbychat"`), *not* the `_name._tcp` Bonjour form used in `NSBonjourServices`. Passing a string in the `_..._tcp` form or over 15 characters throws at startup via `NearbyConnectionsOptionsValidator.ios.cs`. Apps must still declare `NSBonjourServices` in `Info.plist` — the two values are just no longer required (or expected) to match.

## Project Strategy

This project follows an "Anti-Detail-Trap Strategy" focusing on shipping early and iterating fast (`docs/PROJECTPLAN.md`). Current release: `0.3.0-preview.2` — pre-release versioning, tagging, and NuGet publishing are automated via `scripts/release.sh` and `.github/workflows/publish.yml` (see `CONTRIBUTING.md` for the full release process).

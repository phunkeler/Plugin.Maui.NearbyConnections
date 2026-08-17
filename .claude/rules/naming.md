---
paths:
  - "src/Plugin.Maui.NearbyConnections/**/*.cs"
  - "src/Plugin.Maui.NearbyConnections/PublicAPI/**/*.txt"
---

# Naming and structure contract

Binding for every type, member, and folder in `src/Plugin.Maui.NearbyConnections/`. Where this and
habit disagree, this wins. Where this and **verified repo evidence** disagree, this document is the
bug — fix it here in a commit rather than working around it.

Rationale, history, and the open questions live in `DESIGN-PRINCIPLES.md`. This file is the rules
only.

## Vocabulary

| Concept | Term |
|---|---|
| Product / domain | Nearby |
| Remote participant | `NearbyDevice` (never `Peer`) |
| Communication relationship | `NearbyConnection` |
| Finding remote devices | Discovery |
| Making this device findable | Advertising |
| Data crossing a connection | `NearbyPayload` |
| Movement of data | Transfer (distinct from payload) |
| Configuration | `NearbyOptions` |
| Root exception | `NearbyException` |

**Banned from the public contract:** `Session`, `Peer`, `Endpoint`, `MCSession`, `MCPeerID`,
`Browser`, `Advertiser`, `Strategy`, `NativeConnection`, `Radio`, `BluetoothConnection`,
`WiFiConnection`.

Apple having `MCSession` is not a reason to expose a session. Never introduce `INearbySession`,
`NearbySession`, or `Session` — the consumer-facing object is a capability, not a session.

Android's "Google Nearby Connections" and Apple's "Multipeer Connectivity" are the vendors' product
names. Never rename them in prose.

## Operations

```
StartAdvertisingAsync   StopAdvertisingAsync
StartDiscoveryAsync     StopDiscoveryAsync
ConnectAsync   AcceptAsync   RejectAsync   DisconnectAsync
SendAsync      ReceiveAsync
StopAsync
```

Never prefix a method with `Nearby` — the receiver already carries the domain.

```csharp
await nearby.ConnectAsync(device);        // GOOD
await nearby.NearbyConnectAsync(device);  // BAD
```

Never expose platform verbs: `StartBrowsingAsync`, `StartEndpointDiscoveryAsync`,
`StartPeerBrowserAsync`.

## Collision resistance beats brevity

MAUI apps already define `Device`, `Application`, `Connectivity`, `Permissions`. Public types carry
the `Nearby` qualifier wherever a bare noun would collide. Exempt: domain nouns that cannot collide
(`ConnectionRole`). The same exemption covers internal types, where `EndReason` now lives.

Do not rename `NearbyDevice` to `Peer` for networking purity — it is a discovered physical device,
and `Device` is clearer to an app developer.

## Members and locals

The type name carries the domain; don't repeat it in the variable.

```csharp
NearbyDevice device;          // GOOD
NearbyDevice nearbyDevice;    // BAD, unless it genuinely disambiguates
```

Internal names need not be branded — `PeerRegistry`, `PlatformNearby` describe real
responsibilities. Do not churn internals for aesthetic symmetry.

## Payload ≠ transfer

A **payload** is the data; a **transfer** is the act of moving it. Never collapse them.
`connection.ReceiveAsync()` returns `IAsyncEnumerable<NearbyPayload>` — preserve that shape.

```
Payload/   NearbyPayload, NearbyBytesPayload, NearbyFilePayload
Transfer/  NearbyTransferProgress, NearbyTransferTimeoutException, OutgoingTransfer
```

## Namespaces and folders

Every consumer-facing type lives in the single flat package namespace. Never segment namespaces to
mirror folders:

```
Plugin.Maui.NearbyConnections.NearbyConnection               GOOD
Plugin.Maui.NearbyConnections.Connections.NearbyConnection   BAD
```

Folders follow responsibility: `Connections/`, `Devices/`, `Discovery/`, `Payload/`, `Transfer/`,
`Options/`, `Native/`. Never `Interfaces/`, `Models/`, `Services/`, `Managers/`, `Helpers/`,
`Utils/`.

## The platform boundary

```
PUBLIC     INearby, NearbyDevice, NearbyConnection, NearbyPayload, NearbyOptions
                                │
INTERNAL   IPlatformNearby
                        ┌───────┴───────┐
ANDROID  Google Nearby Connections   Multipeer Connectivity  iOS
```

- **Nothing in `Native/` is public.** A `public` type there means the translation layer leaked. This
  is checkable, and that is the point.
- Internal code may use the platform's own vocabulary precisely. Public code may not.
- `Native/` is this plugin's translation layer; `Platforms/` is the MAUI SDK's reserved folder. They
  are different things — never merge or rename one into the other.

### Platform-divergent config is named, not hidden

A knob that exists on one platform only must say so **at the call site**. A setter that silently
does nothing is a defect. All three PublicAPI baselines stay identical — that is the
machine-checkable form of this rule.

```csharp
options.DisplayName = "Kitchen iPad";
options.Android.Topology = NearbyTopology.Star;
options.Apple.EncryptionPreference = NearbyEncryptionPreference.Required;
```

Two traps when editing this area:

- Inside the options type, the `Android` property **shadows the root `Android` namespace**. The
  Android partial needs `this.Android` for the property and `global::Android.Gms…` for the
  namespace. Omitting either produces an error pointing nowhere near the cause.
- Scope objects are get-only with an initialiser, so they cannot be swapped or shared between
  options instances.

## Device state: `Status` to display, the session to act

`NearbyDevice` carries only what describes the device — `Status` and a nullable `Role`. A live
`NearbyConnection` is **not** on the device: it lives in the session, keyed by device id.

```csharp
if (device.Status is NearbyDeviceStatus.Visible)             // filter/display
if (nearby.TryGetConnection(device.Id, out var connection))  // act
```

The reason is threading, not taste. A connection must be readable from any thread, and a keyed
lookup on the session is thread-safe by construction where a field on a bindable object is not.

`NearbyDevice` is an **immutable `sealed record`**. It raises no `PropertyChanged` and implements no
`INotifyPropertyChanged`: a status change produces a *new* snapshot, published as an `Updated` entry
on `Devices.Changes`. Binding goes through `NearbyDeviceCollection`, which is the only type in the
library that knows a UI thread exists.

Two invariants, both easy to regress:

- **A device is only ever replaced, never mutated.** Every transition goes through
  `NearbyImplementation.Transition`, which rewrites the registry entry with `current with { … }` and
  returns the existing instance unchanged when nothing differs. Adding a settable property to
  `NearbyDevice` would reintroduce the shared-mutable-state problem the record exists to remove.
- **The session removes a connection only from the watcher that owns it**, comparing the connection
  by reference as it removes (`_activeConnections.TryRemove(new KeyValuePair<…>(id, connection))` in
  `WatchDisconnectAsync`). Clearing the dictionary anywhere else makes that removal fail its
  identity check, and the device is never returned to `Visible`.

There was formerly a `DeviceState` hierarchy carrying the connection on a `Connected` case, and a
`Status` projected from it. Both are gone; do not reintroduce either. There were also `PropertyChanged`
notifications on a mutable `NearbyDevice`, and a `ConnectionDropped` event — also gone, and also not
to be reintroduced.

## Vendor-neutral names that mirror real native concepts stay

A public name that *reads* oddly is not automatically wrong — check what it maps to before renaming.

- `NearbyConnectionType` (`Balanced`/`HighBandwidth`/`NonDisruptive`) maps to Google's real
  `SetConnectionType()`, a genuinely distinct knob from `Strategy`. **Keep it.**
- `NearbyTopology` is neutral by design and Android-only in effect, documented as such on both the
  enum and the property. **Keep it.**

## The rename gate

The published identity — `PackageId`, `AssemblyName`, `RootNamespace`, repo name — is **locked
through 1.0**. Any rename proposal that is not the single coordinated change described in
`DESIGN-PRINCIPLES.md` is rejected by default. Internal type names, file names, and folders are not
locked and may be reorganised freely.

## When adding a public API

Anything newly `public` fails the build until recorded in
`src/Plugin.Maui.NearbyConnections/PublicAPI/{tfm}/PublicAPI.Unshipped.txt`. Build, read the RS0016
errors, add the listed lines. **Never suppress the analyzer to go green.**

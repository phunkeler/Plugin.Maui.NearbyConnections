# DESIGN-PRINCIPLES.md

The naming and structure contract for this plugin. Authoritative: where this document and a habit
disagree, this document wins. Where this document and **verified repo evidence** disagree, that is a
bug in this document — fix it here, in a commit, rather than working around it.

Every claim below is marked:

- **RULE** — binding. Follow it.
- **TARGET** — stated intent the code has not fully reached. The code is what's wrong, not the rule.
- **OPEN** — deliberately undecided. Do not resolve silently; raise it.

Status of the repo against this document is tracked in §12.

---

## 1. The one-line principle

**Nearby is the product domain. P2P is the communication model. Google Nearby Connections and
Multipeer Connectivity are implementation details.**

Two corollaries, both **RULE**:

- The public API describes what the developer is trying to accomplish, not how Android or iOS
  happens to accomplish it.
- Names are not optimised for theoretical purity at the expense of consumer collision resistance.

## 2. Identity

The gate covers **published identity only**. Internal naming is free.

| Surface | Current | Status |
|---|---|---|
| `PackageId` | `Plugin.Maui.NearbyConnections` | 🔒 locked — §2.1 |
| `AssemblyName` | `Plugin.Maui.NearbyConnections` | 🔒 locked — §2.1 |
| `RootNamespace` / public namespace | `Plugin.Maui.NearbyConnections` | 🔒 locked — §2.1 |
| Repository | `phunkeler/Plugin.Maui.NearbyConnections` | 🔒 locked — §2.1 |
| Primary service | `INearby` | ✅ done |
| Implementation | `NearbyImplementation` | ✅ done |
| Options / root exception | `NearbyOptions` / `NearbyException` | ✅ done |
| Type names, file names, folders | free | not gated |

Eventual target for the locked row is `Plugin.Maui.Nearby`, executed once per §2.1.

**`AssemblyName` and `RootNamespace` are pinned explicitly in the csproj.** They otherwise derive
from the project filename, so renaming the project file would silently change the assembly and
every public namespace — consumer-visible breakage with no diff in the file that caused it. Pinning
them is what makes the rest of the tree safe to reorganise.

The phrase "Nearby Connections" survives **only** where it names Google's actual technology.

```
GOOD  Android uses Google Nearby Connections.
BAD   Nearby Connections is the abstraction exposed by this library.
```

### 2.1 The rename is gated — RULE

The plugin has been renamed twice already (`NearbyConnections` → `NearbyDevices`, 2026-07-22 →
reverted 2026-07-26). The revert's decision record concluded the recurring issue is *"not a naming
problem — it's a decision-commitment problem."*

`.building/notes/BACKLOG.md` #3 carries a **binding finality guard**: a final name is executed
**exactly once**, as a single coordinated change spanning code, NuGet package ID (new package plus a
deprecation pointer on the old), GitHub repo, docs, CI, and the Sonar key — then locked through 1.0.

> Any rename proposal before 1.0 that is not this one-shot coordinated change is rejected by default.

**Scope of the gate (narrowed 2026-08-09):** it covers the four locked rows in §2 — package id,
assembly name, root/public namespace, repo — and nothing else. Internal type names, file names, and
folder layout were never what the finality guard was protecting; the guard exists because a
*published* identity change orphans consumers, and none of those do. They may be reorganised freely
to reach §17's layout, and largely already have been.

The package carries published history (`0.0.0-alpha` → `0.3.0-preview.1`), and the
`v0.3.0-preview.1` tag shipped `INearbyConnections` and `UseNearbyConnections()` to real consumers.
`<PackageId>` is pinned in the csproj so no project-file rename can silently orphan it.

**If the project moves to a new repository and a new package**, the orphaning constraint disappears
and §2's target may be adopted directly at that point. That is currently under consideration and is
the most likely path to §2 becoming real.

## 3. Vocabulary — RULE

| Concept | Term | Note |
|---|---|---|
| Product / domain | Nearby | |
| Remote participant | `NearbyDevice` | Not `Peer` — see §5 |
| Communication relationship | `NearbyConnection` | |
| Finding remote devices | Discovery | |
| Making this device findable | Advertising | |
| Data crossing a connection | `NearbyPayload` | |
| Movement of data | Transfer | Distinct from payload — see §6 |
| Configuration | `NearbyOptions` | ✅ done 2026-08-09 |
| Root exception | `NearbyException` | ✅ done 2026-08-09 |
| Android native tech | Google Nearby Connections | Never rename |
| iOS native tech | Multipeer Connectivity | Never rename |

Banned from the cross-platform contract unless an implementation genuinely requires them:
`Session`, `Peer`, `Endpoint`, `MCSession`, `MCPeerID`, `Browser`, `Advertiser`, `Strategy`,
`NativeConnection`, `Radio`, `BluetoothConnection`, `WiFiConnection`.

### 3.1 No public `Session` — RULE

Apple having `MCSession` is not a reason to expose a session. The consumer-facing object is a
capability, not a session. Do not introduce `INearbySession`, `NearbySession`, or `Session`.

*Repo status: satisfied — zero `Session` types on the public surface across all three TFMs.*

## 4. Operations — RULE

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

*Repo status: satisfied as of 2026-08-09.* `StartDiscoveringAsync`/`StopDiscoveringAsync` became
`StartDiscoveryAsync`/`StopDiscoveryAsync`, and the internal `PlatformStart/StopDiscoveringAsync`
partials followed so the vocabulary is consistent from facade to platform.

## 5. Collision resistance beats brevity — RULE

MAUI applications already define `Device`, `Application`, `Connectivity`, `Permissions`. Public types
here carry the `Nearby` qualifier where a bare noun would collide.

`NearbyDevice` specifically: the object is a discovered physical device, and `Device` is clearer to an
application developer than `Peer`. **Do not rename it to `Peer` for networking purity.**

*Resolved 2026-08-09:* the payload pair was the last unprefixed public inconsistency and is now
`NearbyBytesPayload` / `NearbyFilePayload`. They were the two most collision-prone names on the
surface — an app that models its own payloads is likely to define `FilePayload` itself.

## 6. Payload and transfer are different things — RULE

A **payload** is the data. A **transfer** is the act of moving it. Do not collapse them.

`connection.ReceiveAsync()` returning `IAsyncEnumerable<NearbyPayload>` is the intended shape and is
already implemented. Preserve it.

*Repo status: satisfied as of 2026-08-09.* The concepts now have separate folders:

```
Payload/   NearbyPayload.cs, NearbyBytesPayload.cs, NearbyFilePayload.cs
Transfer/  NearbyTransferProgress.cs, NearbyTransferTimeoutException.cs, OutgoingTransfer.cs
```

All three payload types previously shared `Transfer/NearbyPayload.cs`, so the folder said the
opposite of this rule.

## 7. The platform boundary is visible and checkable — RULE

```
PUBLIC        INearby, NearbyDevice, NearbyConnection, NearbyPayload, NearbyOptions
                                  │
INTERNAL      IPlatformNearbyConnections
                          ┌───────┴───────┐
ANDROID  Google Nearby Connections    Multipeer Connectivity  iOS
```

- **Nothing in `Native/` is public.** A `public` type declared there means the translation layer has
  leaked. This is checkable, and that is the point.
- Internal code may use the platform's own vocabulary precisely. Public code may not.
- `Native/` is this plugin's translation layer; `Platforms/` is the MAUI SDK's reserved convention
  folder. They are different things and are not to be merged or renamed into each other.

### 7.1 Platform-divergent configuration is named, not hidden — RULE

A configuration knob that exists on one platform only must say so **at the call site**, not in a doc
comment alone. A setter that silently does nothing is a defect.

*Repo status: satisfied as of 2026-08-09.* `NearbyConnectionsOptions` exposes two platform scopes,
`NearbyAndroidOptions` and `NearbyAppleOptions`, present on every TFM:

```csharp
options.DisplayName = "Kitchen iPad";
options.Android.Topology = NearbyTopology.Star;
options.Apple.EncryptionPreference = NearbyEncryptionPreference.Required;
```

Previously `Topology`, `UseLowPower`, and `ConnectionType` existed only on `net10.0-android` and
`EncryptionPreference` only on `net10.0-ios`, so shared code could not set them without `#if`. All
three PublicAPI baselines are now identical, which is the machine-checkable form of this rule.

Two consequences worth knowing before editing this area:

- Inside `NearbyConnectionsOptions`, the `Android` property **shadows the root `Android` namespace**.
  The Android partial needs `this.Android` for the property and `global::Android.Gms…` for the
  namespace. Omitting either produces an error that points nowhere near the cause.
- The scope objects are get-only with an initialiser, so they cannot be swapped or shared between
  options instances.

## 8. Namespaces are API architecture; folders are organisation — RULE

All consumer-facing types live in the single package namespace. Do **not** segment namespaces to
mirror folders:

```
Plugin.Maui.Nearby.NearbyConnection      GOOD
Plugin.Maui.Nearby.Connections.NearbyConnection   BAD
```

*Repo status: satisfied — every folder already declares the flat namespace.*

Folders follow responsibility, not type-name symmetry. `Connections/`, `Devices/`, `Discovery/`,
`Transfer/`, `Options/`, `Native/` are correct. `Interfaces/`, `Models/`, `Services/`, `Managers/`,
`Helpers/`, `Utils/` are not.

**OPEN:** whether payload types move from `Transfer/` into their own `Payload/` folder. §6 says the
concepts are distinct; the folder does not yet reflect that. Low value on its own — fold it into the
§2.1 rename if it happens at all.

## 9. Members and locals — RULE

The type name carries the domain. Do not repeat it in the variable.

```csharp
NearbyDevice device;          // GOOD
NearbyDevice nearbyDevice;    // BAD, unless it genuinely disambiguates
```

Internal implementation names need not be branded. `NearbyConnectionsImplementation`,
`PlatformNearbyConnections`, `PeerRegistry`, `LocalPeerIdentityStore` are fine as internal names that
describe their real responsibility. Do not churn internals for aesthetic symmetry.

## 10. Device state: `State` to act, `Status` to display — RULE

`NearbyDevice` deliberately exposes two representations. This is a facade/detail pair, **not**
duplication, and it is not to be collapsed.

- `State` → `DeviceState` hierarchy. Pattern-match it to reach `Role` or `Connection`.
- `Status` → `NearbyDeviceStatus` enum. A derived projection, for display and filtering.

```csharp
if (device.State is DeviceState.Connected { Connection: var connection })   // act
if (device.Status is NearbyDeviceStatus.Visible)                            // filter/display
```

Two binding rules, both already implemented and both easy to regress:

- **Every `State` change raises `PropertyChanged` for `State` *and* `Status`.** Consumers filter by
  property name; raising only `State` freezes bound UI with no compile error and no test failure.
- **The `Status` projection throws on an unrecognised state.** C# 14 has no exhaustiveness checking
  for sealed hierarchies, so a silent `default` arm would report a wrong lifecycle position forever.

## 11. Vendor-neutral names that mirror real native concepts stay — RULE

A public name that *reads* oddly is not automatically wrong. Check what it maps to before renaming it.

`NearbyConnectionType` (`Balanced` / `HighBandwidth` / `NonDisruptive`) reads like a performance
preference rather than a "type". It maps to Google's actual `SetConnectionType()`, which is a
genuinely distinct knob from `Strategy`. The neutral enum exists precisely so consumers never
reference `Android.Gms.Nearby.Connection.Strategy`. **Keep the name.**

`NearbyTopology` likewise: neutral by design, Android-only in effect, and already documented as such
on both the enum and the property. **Keep it.**

## 12. Open decisions — do not resolve silently

Per §1, these are deliberately undecided. Raise them; don't pick.

| # | Decision | Current lean |
|---|---|---|
| 1 | Primary interface name | `INearby` |
| 2 | Device noun — `NearbyDevice` / `NearbyPeer` / `NearbyParticipant` | `NearbyDevice` (§5) |
| 3 | Payload prefixes — `BytesPayload` vs `NearbyBytesPayload` | undecided — see below |
| 4 | Exception hierarchy — prefixed or bare derived types | keep prefixed — see below |
| 5 | `Payload/` as its own folder | low value alone (§8) |
| 6 | Final name + whether a new repo/package is created | gated by §2.1 |

**#3 — the payload pair is the only unprefixed public inconsistency.** Every other public type is
`Nearby`-qualified or is a domain noun that cannot collide (`DeviceState`, `ConnectionRole`,
`EndReason`). `BytesPayload` and `FilePayload` are the exception, and they are the two most likely
to collide in an app that already models its own payloads. §5 argues for prefixing; the counter is
that they are always reached through `NearbyPayload`, so IntelliSense already scopes them. Decide
with the §2.1 rename, when the whole surface moves at once.

**#4 — the hierarchy is already consistent; the lean is to keep it.** Verified shape:

```
NearbyConnectionsException            (base, project root — not sealed, consumers may derive)
├── NearbyAdvertisingException        Discovery/
├── NearbyDiscoveryException          Discovery/
├── NearbyConnectionTimeoutException  Connections/
└── NearbyTransferTimeoutException    Transfer/
```

All four derived types are `sealed` and `Nearby`-prefixed, and each now sits in the folder its
domain owns. The source spec floated dropping the prefixes (`AdvertisingException`,
`DiscoveryException`); §5's collision-resistance rule argues against it — `DiscoveryException` and
`TransferException` are exactly the generic names an app is likely to define itself. The only name
that changes at §2.1 is the base, `NearbyConnectionsException` → `NearbyException`.

## 13. Repo status against this document

Full audit 2026-08-09. Every row was checked by command against the `net10.0-android` baseline
(the widest surface), not by reading.

| § | Rule | Status | How it was checked |
|---|---|---|---|
| 1 | No vendor types on the public surface | ✅ | 0 hits for `Android.Gms`/`MultipeerConnectivity`/`MCSession`/`MCPeerID` in any baseline |
| 2 | Published identity locked, internals free | ✅ | `PackageId`/`AssemblyName`/`RootNamespace` pinned in csproj |
| 3 | Banned vocabulary absent from public API | ✅ | 0 public types matching `Peer`/`Endpoint`/`Browser`/`Advertiser`/`Strategy`/`Session`/`Radio` |
| 3 | `NearbyOptions` / `NearbyException` | ✅ | renamed 2026-08-09 |
| 3.1 | No public `Session` | ✅ | 0 hits, all three TFMs |
| 4 | No `Nearby`-prefixed methods | ✅ | 0 hits for `.Nearby*Async(` |
| 4 | `StartDiscoveryAsync` naming | ✅ | renamed 2026-08-09, incl. internal partials |
| 5 | Collision-resistant public nouns | ✅ | every public type is `Nearby`-qualified except `DeviceState`/`ConnectionRole`/`EndReason`, which cannot collide |
| 6 | Payload ≠ transfer | ✅ | `ReceiveAsync` → `IAsyncEnumerable<NearbyPayload>` |
| 7 | Nothing public in `Native/` | ✅ | 0 public declarations in `Native/*.cs` |
| 7.1 | Platform config named at the call site | ✅ | `Android`/`Apple` scopes; all three baselines identical |
| 8 | Flat namespaces | ✅ | every folder declares `Plugin.Maui.NearbyConnections` |
| 8 | Folders follow responsibility | ✅ | fixed 2026-08-09 — see below |
| 9 | No redundant `nearbyX` locals | ✅ | 1 hit, a pattern-match binding where the name disambiguates (permitted) |
| 10 | `State`/`Status` invariants | ✅ | dual `PropertyChanged` raise + throwing projection, `NearbyDevice.cs:106-134` |
| 11 | Vendor-neutral names that mirror native concepts | ✅ | `NearbyConnectionType`, `NearbyTopology` kept |

**Fixed during this audit (§8):** the transfer timeout exception was declared inside the base
exception's own file — a public type in a file named for a different type, and a *transfer*
exception filed under `Connections/`. The base exception, whose own doc calls it "the base class
for every exception raised by this library", also sat inside one domain folder. Now: base at the
project root beside the facade, transfer exception in `Transfer/`. Its test file covered only
advertising/discovery exceptions, so it moved to `Discovery/` to mirror its subject.

**Not violations, recorded so they are not re-flagged:** `NearbyDeviceEvent`,
`NearbyDeviceEventType`, `ControlMessage`, and `NearbyConnectionRequest` are all `internal`. An
earlier draft of the source spec implied some were public; they are not.

**Outstanding: only the four locked identity rows in §2** — package id, assembly name, root
namespace, repo. Every other rule in this document is satisfied.

### Source layout

The tree now matches the target layout except where §2 is locked:

```
INearby.cs  NearbyImplementation.{cs,log,state}.cs  NearbyException.cs
MauiAppBuilderExtensions.cs  ServiceCollectionExtensions.cs
Connections/  Devices/  Discovery/  Payload/  Transfer/  Options/  Native/  Platforms/
```

Two deliberate departures from the source spec's §17 tree, both because §17 was wrong:

- **`NearbyException` sits at the project root, not in `Connections/`.** §17 filed the root
  exception inside one domain folder, contradicting its own description of it as the base for the
  whole library.
- **`Native/` uses `IPlatformNearby` / `PlatformNearby.*`.** §17 kept the old
  `PlatformNearbyConnections` names, which would have left the internal layer reading against a
  facade now called `INearby`.

Verified 2026-08-09: three TFMs build warning-free, 208/208 unit tests pass, sample builds on
Android and iOS, and all three PublicAPI baselines are identical.

## 14. Working rules for contributors and agents

1. Read this document before making naming or structural changes.
2. Inspect the current branch rather than assuming it matches this document — §13 is a summary and
   goes stale.
3. This is a design target, not authorisation to resolve §12.
4. Keep public terminology platform-neutral; internal terminology may be precise about the platform.
5. Prefer incremental commits for large refactors; keep behaviour changes separate from naming changes.
6. Anything newly `public` fails the build until recorded in
   `src/…/PublicAPI/{tfm}/PublicAPI.Unshipped.txt`. Never suppress RS0016 to go green.
7. Report unresolved terminology decisions at the end of a task rather than deciding them.

---

## Provenance

Derived from a working specification drafted with ChatGPT (2026-08-09), vetted against the repo
before adoption. Corrections applied during vetting:

- **Rename premise** — the source assumed the rename was a clean redesign with no shipped history. It
  omitted the published NuGet package, the two prior renames, and the BACKLOG #3 finality guard. Its
  proposed eleven-commit staged sequence is the exact shape that guard rejects. Replaced by §2.1.
- **`ConnectionType`** — the source proposed renaming it to `ConnectionPreference`/`ConnectionMode`.
  Contradicted by `Native/PlatformNearbyConnections.android.cs:35`. Became §11.
- **`OutgoingTransfer`** — the source asked whether it belongs on the public API. It is already
  `internal`. Question dropped.
- **`Session`** — the source warned against reintroducing it. It was never there. Recorded as
  satisfied rather than pending.
- **Options TFM asymmetry** — the source was silent; the defect is visible only by diffing the three
  PublicAPI baselines. Added as §7.1.
- **`State`/`Status`** — the source never mentions `NearbyDeviceStatus` despite it being public.
  Added as §10, including the two regression-prone invariants.

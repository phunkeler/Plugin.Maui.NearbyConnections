# Device Lifecycle — Capability Model

**Purpose:** Model the *pre-connection* phase (advertise / discover / request / accept / reject /
expire) as a first-class capability, distinct from established connections — and state honestly
which parts are native on both platforms, native on one, or a plugin extension.

This is a design reference for contributors. It documents what each platform SDK actually
guarantees, where they diverge, and which behaviour the plugin supplies itself.

> **`INearby` is the source of truth for the API.** This document explains platform behaviour and
> the reasoning behind the model; it does not define the surface. Gaps 2, 3, and 4 are closed. Gap 1
> (a uniform failure reason) remains open — see `docs/PLATFORM-ABSTRACTION-REVIEW.md` §3 for the
> tracking home, not the GitHub issues.

---

## Consumer-facing summary

A device has one of four states: `Visible`, `RequestReceived`, `Connecting`, `Connected`. That's
the whole model — no platform internals needed to use it.

```mermaid
flowchart LR
    classDef stateBox fill:#e8f0fe,stroke:#4285f4,stroke-width:2px,rx:10,ry:10,color:#1a1a1a,font-weight:bold
    classDef pending fill:#fef7e0,stroke:#f9ab00,stroke-width:2px,rx:10,ry:10,color:#1a1a1a,font-weight:bold
    classDef live fill:#e6f4ea,stroke:#34a853,stroke-width:2px,rx:10,ry:10,color:#1a1a1a,font-weight:bold
    classDef ghost fill:#f1f3f4,stroke:#9aa0a6,stroke-width:1px,stroke-dasharray:4 3
    classDef note fill:#ffffff,stroke:#9aa0a6,stroke-width:1px,color:#5f6368,font-style:italic

    Start(( )):::ghost -->|discovered| Visible["Visible"]:::stateBox
    Visible -->|inbound request| RequestReceived["RequestReceived"]:::stateBox
    Visible -->|"ConnectAsync()"| Connecting["Connecting"]:::pending
    RequestReceived -->|"AcceptAsync()"| Connecting
    RequestReceived -->|"RejectAsync()"| Visible
    Connecting -->|handshake succeeded| Connected["Connected"]:::live
    Connecting -->|"rejected / timed out / failed"| Visible
    Connected -->|"DisconnectAsync() or peer disconnected"| Visible
    Visible -->|out of range| Gone(( )):::ghost

    ConnectingNote["Advisory, not guaranteed:<br/>on iOS a peer can skip straight<br/>to Visible without ever<br/>appearing here. Role tells<br/>you the direction."]:::note -.- Connecting
    ConnectedNote["TryGetConnection() returns a<br/>NearbyConnection only while a<br/>device is in this state."]:::note -.- Connected
    GoneNote["Removed from nearby.Devices<br/>entirely — not a status value."]:::note -.- Gone
```

There's no `Failed` or `Rejected` or `Disconnected` state. Reject a request, miss a timeout, lose
the connection — it all lands back on `Visible`. Same bucket as a device you just found.

Devices only disappear from `nearby.Devices` when they walk out of range while `Visible`. A device
mid-handshake doesn't get evicted mid-flight — a failed handshake drops it back to `Visible` first,
then it can be lost normally from there.

One thing to watch: `Connecting` isn't guaranteed on iOS. A peer can jump straight from a request
back to `Visible` without you ever seeing `Connecting` in between. Don't build logic that assumes
you'll see it.

The per-platform detail — what each SDK actually calls, and the two sequence diagrams for Android
vs. iOS — is below.

---

## Naming constraint — vendor-neutral public vocabulary

**No public type or member borrows vocabulary from either platform SDK.** This extends the
reasoning that disqualified vendor-branded terms for the package name, extended down to the type
surface.

Measured against the current source, both candidate words are vendor terms:

| Term | Owner | Occurrences in `src/` |
|---|---|---|
| `Peer` | **Apple** — `MCPeerID`, `MCNearbyServiceBrowser`, MPC docs throughout | 36 × `MCPeerID`, plus `PeerKey`/`PeerId` |
| `Endpoint` | **Google** — `EndpointId`, `DiscoveredEndpointInfo`, `EndpointDiscoveryCallback` | 27 × `EndpointId`, plus callbacks |

Both are therefore **disqualified** for public API. `Peer` is the more dangerous of the two because
it *sounds* generic while being specifically MPC's term.

**Chosen public noun: `Device`** (`NearbyDevice`, `Devices`, `NearbyDeviceStatus`). Rationale:

1. **Vendor-neutral.** Neither SDK uses "device" as its identity type.
2. **MAUI-idiomatic.** The framework's own vocabulary is `DeviceInfo`, `DeviceDisplay`,
   `IDeviceInfo` — "device" is what .NET MAUI calls a physical thing running an app.
3. **Zero rename cost.** `NearbyDevice` is published API since preview.1. Keeping it means the
   rename table shrinks and the `Devices` naming stays consistent rather than flip-flopping.

**Internal code keeps `Peer`.** `PeerRegistry`, `PeerKeyProvider`, `PeerIdArchive` are `internal`,
never seen by a consumer, and sit at the layer that genuinely talks to `MCPeerID`. Using the
platform's word there is *correct*, not a leak. The rule is: public
says `Device`, internal says `Peer`, and the public/internal boundary is the enforcement mechanism.

> Candidates considered and rejected: `NearbyPeer` (Apple's term), `NearbyEndpoint` (Google's term),
> `NearbyParticipant` (neutral and semantically precise, but long, and `Participants` reads oddly
> for devices you have not connected to), `NearbyNode` (neutral but reads as mesh/graph
> infrastructure, unusual in MAUI app code).

---

## Why the model is shaped this way

A model that exposes only *visible* devices and *established* connections captures the two stable
endpoints and skips the negotiation phase between them — which is where essentially all of the
difficulty in P2P lives: two-sided handshakes, rejection, timeout, expiry, and a peer vanishing
mid-negotiation.

That is why negotiation is first-class here. A single `Devices` collection carries the whole
lifecycle, and `NearbyDeviceStatus` moves through it in place, so a bound row updates rather than
migrating between collections. Two collections could disagree with each other; one cannot.

The plugin owns both platforms' state machines, so the consumer does not rebuild them. Before this
model shipped, the sample hand-rolled the missing states — an `IsConnecting` flag with manual unwind
on failure, and a pending-inbound list rebuilt from replayed events on every navigation. Both are
now plugin state, which is the concrete measure of what this model buys.

---

## The peer lifecycle

```mermaid
stateDiagram-v2
    [*] --> Visible : discovered

    Visible --> RequestReceived : inbound invitation
    Visible --> Connecting : ConnectAsync() (outbound)

    RequestReceived --> Connecting : AcceptAsync()
    RequestReceived --> Visible : RejectAsync()
    RequestReceived --> Visible : request expired

    Connecting --> Connected : handshake succeeded
    Connecting --> Visible : rejected / timed out / failed

    Connected --> Visible : disconnected, still in range
    Connected --> [*] : disconnected and out of range

    Visible --> [*] : lost (out of range)
    RequestReceived --> [*] : peer vanished
    Connecting --> [*] : peer vanished

    note right of RequestReceived
        Inbound: they asked us.
        Carries the accept/reject handle.
    end note

    note right of Connecting
        Covers BOTH directions:
        - outbound (we called ConnectAsync)
        - inbound accepted (we called AcceptAsync)
        Direction is available via Role.
    end note

    note left of Visible
        iOS may skip Connecting entirely and go
        straight to NotConnected (see Verified
        platform behaviour, finding 2). Connecting
        is therefore NOT a guaranteed waypoint.
    end note
```

Five states. A two-collection model covers only `Visible` and `Connected`; the three middle states
are where the sample's hand-rolled code lives.

### ⚠ `Connecting` is not a guaranteed waypoint

Verified against Apple's own DTS guidance (see *Verified platform behaviour* below): on iOS a peer
can transition **directly from the invitation to `NotConnected`, never entering `Connecting`** — both
on a declined invitation (the common path) and, rarely, on error. Any implementation that treats
`Connecting` as a required transition, a gate, or the only place a pending flag gets reset **has a
latent hang**. The state machine above must be read as "`Connecting` is optional on every path that
passes through it."

This is not hypothetical for this codebase: it is a bug that has been hit and fixed before
(iOS `NotConnected` must fault the pending TCS or `AcceptAsync`/`ConnectAsync` hang forever), and
the current `NearbyConnections.ios.cs` handles it correctly by faulting on `NotConnected` without
requiring `Connecting` to have occurred. **Any refactor must preserve that property.**

---

## Platform mapping

### Android — Google Nearby Connections

```mermaid
sequenceDiagram
    participant A as App
    participant P as Plugin
    participant G as Nearby SDK

    G->>P: OnEndpointFound(endpointId, info)
    P->>A: Status = Visible

    alt inbound (they invited us)
        G->>P: OnConnectionInitiated(IsIncomingConnection = true)
        P->>A: Status = RequestReceived
        A->>P: AcceptAsync()
        P->>G: AcceptConnection()
        P->>A: Status = Connecting
    else outbound (we invited them)
        A->>P: ConnectAsync(device)
        P->>P: register TCS, start ConnectTimeout
        P->>G: RequestConnection()
        P->>A: Status = Connecting
        G->>P: OnConnectionInitiated(IsIncomingConnection = false)
        P->>G: AcceptConnection() (auto)
    end

    alt success
        G->>P: OnConnectionResult(Status.IsSuccess = true)
        P->>A: Status = Connected
    else failure
        G->>P: OnConnectionResult(StatusCode, StatusMessage)
        P->>A: Status = Visible
        Note over P,A: NearbyException carries<br/>StatusCode and StatusMessage
    else ConnectTimeout elapsed
        Note over P,G: No callback ever arrives. The plugin-owned<br/>deadline is the only thing that ends the wait.
        P->>G: DisconnectFromEndpoint() (clear GMS state)
        P->>A: Status = Visible
        Note over P,A: NearbyConnectionTimeoutException
    end

    G->>P: OnDisconnected(endpointId)
    P->>A: Status = Visible
    G->>P: OnEndpointLost(endpointId)
    P->>A: removed
```

### iOS — MultipeerConnectivity

```mermaid
sequenceDiagram
    participant A as App
    participant P as Plugin
    participant M as MultipeerConnectivity

    M->>P: FoundPeer(peerID, info)
    P->>A: Status = Visible

    alt inbound (they invited us)
        M->>P: DidReceiveInvitationFromPeer(invitationHandler)
        P->>A: Status = RequestReceived
        A->>P: AcceptAsync()
        P->>M: invitationHandler(true, session)
    else outbound (we invited them)
        A->>P: ConnectAsync(device)
        P->>M: InvitePeer(peerID, session, timeout: ConnectTimeout)
    end

    opt not guaranteed
        M->>P: DidChangeState(Connecting)
        P->>A: Status = Connecting
    end

    alt success
        M->>P: DidChangeState(Connected)
        P->>A: Status = Connected
    else failure
        M->>P: DidChangeState(NotConnected)
        Note over M,P: NO REASON PROVIDED — rejection,<br/>timeout, and range-loss are indistinguishable
        P->>A: Status = Visible
        Note over P,A: NearbyException
    else ConnectTimeout elapsed
        Note over M,P: MPC can hang in Connecting with neither<br/>terminal callback arriving
        P->>A: Status = Visible
        Note over P,A: NearbyConnectionTimeoutException
    end

    M->>P: LostPeer(peerID)
    P->>A: removed
```

---

## Capability matrix

Legend: **N** = native on both · **N¹** = native on one, synthesized on the other ·
**X** = plugin extension (no native equivalent) · **⚠** = asymmetric fidelity, must be documented

| Capability | Android | iOS | Class | Notes |
|---|---|---|---|---|
| Discover / lose a peer | `OnEndpointFound` / `OnEndpointLost` | `FoundPeer` / `LostPeer` | **N** | Clean parity. |
| Distinguish inbound vs outbound | `ConnectionInfo.IsIncomingConnection` | implicit (invitation callback vs. `InvitePeer`) | **N** | iOS is implicit but unambiguous at the call site. |
| Observe "negotiating" | `OnConnectionInitiated` | `MCSessionState.Connecting` | **N** | Both expose it; neither is surfaced today. |
| Established connection | `OnConnectionResult(IsSuccess)` | `MCSessionState.Connected` | **N** | |
| **Failure reason** | ✅ `StatusCode` + `StatusMessage` | ❌ bare `NotConnected` | **⚠** | **The key asymmetry.** Android can say *why*; iOS cannot. |
| Distinguish "rejected" from "timed out" | ✅ via status code | ❌ indistinguishable | **⚠** | On iOS both collapse to `Unknown`. |
| Connection timeout | ❌ no native timeout | ✅ `InvitePeer(timeout:)`, inviting side only | **N¹** | Plugin-owned deadline on both platforms: `ConnectTimeout` (30s) for `ConnectAsync`, `AcceptTimeout` (15s) for `AcceptAsync`. Neither platform bounds the accepting side at all. |
| Inbound request expiry | ❌ | ❌ | **X** | Neither platform expires a *pending inbound* request. Plugin extension: `InboundRequestTimeout` (30s default) withdraws it, and `StopAsync` rejects whatever is still outstanding. |
| Pending state survives navigation | ❌ | ❌ | **X** | Both platforms are callback-only. Observable collection is the plugin's contribution. |
| Per-peer disconnect | ✅ `DisconnectFromEndpoint` | ❌ `MCSession.Disconnect()` tears down the whole session | **⚠** | Documented by expo-nearby-connections as a known divergence. |

### Verified platform behaviour (iOS / MultipeerConnectivity)

Confirmed against Microsoft Learn and Apple Developer Forums (DTS engineer responses), not inferred
from our wrapper. These five findings constrain the design:

1. **`MCSessionState` has exactly three cases** — `NotConnected` (0), `Connecting` (1),
   `Connected` (2). State is **per-peer**, delivered via the required
   `IMCSessionDelegate.DidChangeState(session, peerID, state)`.
2. **`Connecting` is not guaranteed to occur.** A peer can go directly to `NotConnected` — on a
   declined invitation (expected) and, rarely, on error. Apple DTS: *"detecting declined invites or
   dropped invites is something that is not well supported in these APIs… a lot of this ends up
   having to be handled with manual logic."* **This is the single most important constraint in this
   document.**
3. **`NotConnected` is overloaded and carries no `NSError`.** It means all of: declined, handshake
   failed, transport error, peer walked away, remote disconnected. There is no API to disambiguate.
4. **`Connecting` can hang indefinitely.** Documented case: Wi-Fi enabled but unassociated while
   cellular is active — neither `Connected` nor `NotConnected` ever arrives. Timeouts must be
   enforced by the caller via `InvitePeer(timeout:)`, never awaited from the delegate.
5. **`ConnectedPeers` lags the callback.** The departing peer is still in `session.ConnectedPeers`
   when `DidChangeState(…, NotConnected)` fires. (The current code relies on this; see review
   finding P2-1 for the empty-collection edge case it gets wrong.)
6. **Callbacks arrive on background threads** — MS Learn states this explicitly. Every observable
   collection mutation and `PropertyChanged` raise must be marshalled.

**Design consequence:** a `Connecting` status is still worth exposing (it is real, and Android
reports it reliably), but the plugin must drive `Visible`/`Connected` transitions from the
*terminal* callbacks alone, treating `Connecting` as advisory. A timeout the plugin owns — not a
state the platform promises — is what guarantees a peer never sticks in `Connecting` forever.

Sources: [MCSessionState](https://learn.microsoft.com/dotnet/api/multipeerconnectivity.mcsessionstate) ·
[IMCSessionDelegate](https://learn.microsoft.com/dotnet/api/multipeerconnectivity.imcsessiondelegate) ·
[Apple forum 129352](https://developer.apple.com/forums/thread/129352) ·
[Apple forum 811978](https://developer.apple.com/forums/thread/811978)

### The four honest gaps

1. **`EndReason` is richer on Android.** On iOS most failures report `Unknown` natively.
2. **Invitation timeout is native only on iOS.** Android has no native timeout.
3. **Per-peer disconnect is not achievable on iOS natively.** `MCSession.Disconnect()` is all-or-nothing.
4. **Inbound request expiry exists on neither platform.**

---

## Closing the gaps — a genuinely uniform API

Three of the four gaps close cleanly. The mechanism already exists in this codebase:
**`ControlMessage`** (`Connections/ControlMessage.cs`), an application-level wire protocol already
used to signal disconnect on iOS. Extending it turns platform-specific behaviour into plugin-owned,
uniform behaviour.

```mermaid
graph LR
    subgraph Native["What the platforms give"]
        A1["Android: StatusCode<br/>rich reasons"]
        I1["iOS: NotConnected<br/>no reason"]
        A2["Android: no timeout"]
        I2["iOS: InvitePeer timeout"]
        A3["Android: per-peer disconnect"]
        I3["iOS: session-wide only"]
    end

    subgraph Plugin["Plugin-owned layer"]
        CM["ControlMessage protocol<br/><i>Reject / Disconnect / Bye</i>"]
        TM["Plugin-owned timers"]
    end

    subgraph API["Uniform surface"]
        R["EndReason<br/><i>Rejected / TimedOut / Lost / Error</i>"]
        T["ConnectTimeout<br/><i>both platforms</i>"]
        D["peer.DisconnectAsync()<br/><i>both platforms</i>"]
    end

    I1 --> CM --> R
    A1 --> R
    A2 --> TM --> T
    I2 --> T
    I3 --> CM --> D
    A3 --> D

    style Plugin fill:#e6f4ea,stroke:#34a853
    style API fill:#e8f0fe,stroke:#4285f4
```

### Gap 1 — failure reason: **closeable to ~90%**

The insight: iOS can't tell you *why* natively, but **the rejecting peer knows why**. Send it.

```
RejectAsync()  →  ControlMessage.Reject  →  then tear down
```

The receiving side reads `Reject` immediately before `NotConnected` and reports
`EndReason.Rejected` with certainty — on **both** platforms. Timeout is likewise plugin-owned
(gap 2), so `TimedOut` is known. That leaves only genuine transport failure and peer-vanished as
`Lost`/`Error`, which is exactly the distinction Android's status codes give.

> Apple's own DTS engineer recommends precisely this: *"a lot of this ends up having to be handled
> with manual logic"* — an explicit reject message before disconnecting is the sanctioned workaround.

**Residual:** if the peer is killed mid-reject the message never arrives → `Lost`. Honest and rare.

### Gap 2 — invitation timeout: ✅ **CLOSED**

iOS has `InvitePeer(timeout:)`; Android has nothing — confirmed against Google's docs:
`requestConnection`'s Task completes when the request is *sent*, and nothing bounds how long a peer
may take to answer.

**Implemented** in `NearbyConnections.shared.cs` (`ConnectAsync`): `ConnectTimeout` moved from
iOS-only to shared, enforced by a plugin-owned `CancellationTokenSource` timed through the injected
`TimeProvider` so it is testable with `FakeTimeProvider`. Expiry throws
`NearbyConnectionTimeoutException`, distinguishable from caller cancellation. On Android the
timeout also calls `DisconnectFromEndpoint` (`PlatformAbandonConnectAsync`), without which Google
Play Services keeps the endpoint marked connected and every retry fails with
`STATUS_ALREADY_CONNECTED_TO_ENDPOINT`.

**Bonus, as predicted:** this also covers verified-behaviour finding 4 — the documented iOS case
where `Connecting` hangs forever with neither terminal callback arriving.

### Gap 3 — per-peer disconnect: ✅ **CLOSED**

`ControlMessage.Disconnect` is sent to the departing peer, and `MCSession` is torn down only when
the last peer leaves (`NearbyConnections.ios.cs`, the `NotConnected` case — note the
`connectedPeers.Length > 0` guard, which exists because `Enumerable.All` returns `true` for an empty
sequence and without it a failed handshake disposed the session out from under still-connected
peers). `INearby.DisconnectAsync(device)` is the public verb on both platforms.

Original analysis, retained for context:

`ControlMessage.Disconnect` already exists and is already sent on iOS before teardown. Completing it:
the receiver treats `Disconnect` as "this specific peer left," removes only that peer, and keeps the
`MCSession` alive for remaining peers. `MCSession.Disconnect()` is then called only when the *last*
peer departs — which the code already attempts (see review finding P2-1 for the empty-collection bug
to fix while here).

### Gap 4 — inbound request expiry: **closed**

Neither platform expires a pending inbound request. Because it is entirely plugin-owned, it is
uniform for free: one timer, one `EndReason.RequestExpired`, identical on both platforms.

**Implemented** in `NearbyImplementation.state.cs` (`ArmRequestExpiry`, `ExpireRequestAfterAsync`),
bounded by `NearbyOptions.InboundRequestTimeout` (30s default, `Timeout.InfiniteTimeSpan` disables).
Three decisions worth knowing:

- **It expires by rejecting, not by faulting.** Nothing awaits an outstanding request, so there is no
  caller to throw to. Rejecting also releases the platform handle — MPC holds the invitation open
  until its handler is resolved, and GMS refuses a later attempt to a half-open endpoint.
- **The countdown belongs to the request object**, so removing the request from `_pendingRequests`
  and disarming its timer are the same act and cannot be forgotten separately.
- **`NearbyDevice.RequestExpiresAt` publishes the deadline** so a consumer can display a countdown.
  It is a deadline rather than a remaining duration because the device snapshot is immutable.

### Resulting capability matrix

| Capability | Before | After | Mechanism | Status |
|---|---|---|---|---|
| Failure reason | ⚠ Android only | uniform (~90%) | `ControlMessage.Reject` + owned timers | ⏳ **open** (gap 1) |
| Connect timeout | ⚠ iOS only | ✅ uniform | `ConnectTimeout`, both platforms | ✅ **done** |
| Accept timeout | ❌ neither | ✅ uniform | `AcceptTimeout`, both platforms | ✅ **done** |
| Per-peer disconnect | ⚠ Android only | ✅ uniform | `ControlMessage.Disconnect` | ✅ **done** |
| Request expiry | ❌ neither | ✅ uniform | `InboundRequestTimeout`, both | ✅ **done** (gap 4) |
| `Connecting` reliability | ⚠ iOS may skip | ✅ advisory + timeout-backed | Terminal-callback-driven | ✅ **done** |

### What this costs, honestly

- **A wire protocol both ends must speak.** Two devices running different plugin versions could
  disagree. Needs a version byte in `ControlMessage` — cheap now, expensive later.
- **A control message is not free.** It is an extra round trip on reject/disconnect. Negligible
  against handshake cost.
- **`EndReason.Rejected` is best-effort, not guaranteed.** A peer killed mid-reject reports `Lost`.
  Document as best-effort — do **not** claim certainty.
- **Scope.** Gaps 1 and 4 add new capability; gaps 2 and 3 complete work already started.
  Sequencing them is a scope decision, not a technical one.

---

## Open questions

1. ~~`Peers`/`NearbyPeer` vs `Devices`/`NearbyDevice`~~ — **Settled:** `NearbyDevice`/`Devices`.
   Vendor-neutral (both `Peer` and `Endpoint` are SDK terms), MAUI-idiomatic, and keeps published
   API. See *Naming constraint* above.
2. ~~**Does `NearbyConnection` stay a separate type**~~ — **Settled: separate.** Preserves the
   published type and keeps transfer concerns off the device.
3. ~~**Synthesize invitation timeout on Android**~~ — **Settled: synthesized.** See gap 2 above.
4. ~~**Per-peer disconnect on iOS**~~ — **Settled: emulated via `ControlMessage`.** See gap 3.
5. ~~**Does `Connections` survive**~~ — **Settled: no.** One `Devices` collection; consumers filter
   on `Status`. Two collections could disagree.
6. **Vendor-neutrality sweep** — **mostly done.** Zero `Android.Gms` / `MultipeerConnectivity`
   types remain in any PublicAPI baseline (verifiable by grepping them).
   - ~~`Strategy`~~ → **done:** `Topology : NearbyTopology`.
   - ~~`EncryptionPreference`~~ → **done:** `NearbyEncryptionPreference`.
   - ~~`ConnectionType`~~ → **done:** `NearbyConnectionType`. Was a raw `int` holding a Google
     constant — untyped *and* vendor-specific.
   - `NearbyOptions.ServiceId` → neutral enough, but its iOS semantics are Bonjour's
     `serviceType`; keep the name, keep documenting the platform difference.
   - ~~`InvitationTimeout`~~ → **done:** split into `ConnectTimeout`, `AcceptTimeout`, and
     `InboundRequestTimeout`. "Invitation" was MPC vocabulary, and the option is cross-platform,
     which made the leak more visible rather than less. One option could not stay honest once the
     accept path needed its own, shorter window.

7. **`net10.0` cannot enumerate the advertise/discover streams — so `PlatformNearbyTests` reads
   internal channel fields.** `AdvertiseAsync`/`DiscoverAsync` call a `Platform*` start that throws
   `PlatformNotSupportedException` on the headless target, so no test can enumerate past it. The
   ~20 assertions in `test/.../Native/PlatformNearbyTests.cs` therefore read `_advertiseChannel`,
   `_discoverChannel`, `_connectionTcs` and `_activeConnections` directly.

   The concrete cost: `AdvertiseAsync` swaps `_advertiseChannel` via `Interlocked.Exchange` on every
   call, so those tests are correct only because nothing enumerates during them. They cover the
   write side of the bridge and not the swap, and they would keep passing if the swap logic broke.

   Closing it means giving the `net10.0` target a way to enumerate without a platform start — for
   example a stub that yields an empty completed stream rather than throwing. That is a change to
   what platform-unsupported *means*, not a test refactor, which is why it was deliberately left out
   of the test cleanup pass that documented it. Until then the coupling is called out in that
   file's class remarks so it reads as a known exception rather than an accident.

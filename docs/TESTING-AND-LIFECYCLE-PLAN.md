# Testing Strategy & Connection Lifecycle — Plan

Status: **mostly proposed.** §3.6 (iOS lifecycle teardown) shipped 2026-08-04; everything else here
is still proposed. Individual sections state their own status.

Scope: the plugin's test strategy and the remaining connection-lifecycle gaps. Written after
NearbyChat 1:1 was manually verified working end-to-end (text, photo, video, bidirectional
progress) — that validates the happy path and *only* the happy path, which is what makes the
gaps below worth naming now.

Companion documents: [`DEVICE-LIFECYCLE.md`](DEVICE-LIFECYCLE.md) (gap analysis, still accurate),
[`PAYLOAD-DELIVERY.md`](PAYLOAD-DELIVERY.md) (stream design and consumer contract).

---

## 0. Product scope — decided 2026-08-04

**The plugin targets file transfer and pairing: foreground, both-devices-present, bounded
interactions. Not background-surviving chat.**

This was settled after the §3.6 backgrounding work, when the reconnection story prompted a genuine
"is this plugin useful?" question. The answer turned on separating two claims:

- *"The reconnection story is bad"* — **true, and unfixable.** MPC has no background mode and no
  reconnect primitive; GMS poisons endpoints when a process dies. Every MAUI P2P library faces
  exactly these constraints. No amount of plugin work changes them.
- *"Therefore the plugin isn't useful"* — **only if the use case is a long-lived background
  connection.** For transfer and pairing, both users are actively looking at their devices doing the
  thing. Backgrounding means the interaction is over or abandoned. Teardown-on-background is
  *correct behaviour* for that shape, not a limitation.

**Consequences for this document — these reprioritise what follows:**

1. **§3.5 (inbound request expiry) outranks §3.4 (`EndReason`).** §4 previously promoted `EndReason`
   on the grounds that "backgrounding makes it the input any reconnect policy depends on." With
   auto-reconnect explicitly out of scope, that rationale is void. A pairing prompt nobody answers is
   a common, concrete case; request expiry is plugin-owned, uniform across platforms, and
   `FakeTimeProvider`-testable. `EndReason` remains worth having for a different reason: telling a
   user "they declined" versus "they walked out of range."
2. **§3.3 (unbounded receive channel) gets *more* serious.** File transfer is precisely the workload
   that fills it. The replay-versus-bound trade-off is now on the primary path, not an edge case.
3. **NearbyChat demos the wrong shape.** It is a chat sample for a transfer/pairing library — the one
   use case that does not survive a lock screen. A transfer or pairing sample would demonstrate the
   real strength, and is also the cheapest test of whether this scope decision is right: if it is
   straightforward to write and pleasant to use, the scope is correct.

**Not affected by this decision:** the MPC deprecation migration (Apple deprecated MPC in the Xcode
27 beta and DTS now steers people to Network.framework). That remains the more serious long-term
threat and is tracked separately — and Network.framework has real background support paths, so some
of what reads as a dead end here is specifically an *MPC* dead end.

---

## 1. Where testing actually stands

| Layer | Project | Count | Runs on | Verdict |
|---|---|---:|---|---|
| Unit | `Plugin.Maui.NearbyConnections.UnitTests` | 133 | `net10.0`, CI | Healthy. Fast, deterministic, `FakeTimeProvider`-driven. |
| Integration | — | 0 | — | **Missing entirely.** |
| UI / E2E | `NearbyChat.UiTests` | 6 | 3 physical Android devices | Historically fragile; Android-only. |

### 1.1 The missing middle is the real problem

Unit tests substitute `IPlatformNearbyConnections` with `FakeNearbyConnections`, so **everything below the
session is untested by anything that runs in CI**: `NearbyConnections.android.cs`,
`NearbyConnections.ios.cs`, `PeerRegistry` interactions, `ControlMessage` encode/decode against a
real peer, payload materialization, file copy semantics.

That code is reachable only through the 6 UI tests, which need three physical Android devices and
have never all passed simultaneously. **On iOS there is no automated coverage of the platform layer
at all** — the manual 1:1 run is currently the only thing that has ever exercised it.

This is why the payload-delivery regression shipped: it lived exactly in that gap. No unit test
could see it (the session was faked), and no UI test could see it (they were red for unrelated
reasons).

### 1.2 UI test fragility is a known, documented history

Eight distinct bugs were diagnosed and fixed across the suite (locator strategy, cross-test
parallelism, stale connection state, toggle-vs-idempotence, zero-opacity accessibility pruning,
modal sheet dismissal, and more). Best result achieved: **2 of 6 passing**. The tests are valuable —
they found real product bugs — but they are not a dependable gate, and treating them as one has
already cost multiple debugging sessions.

---

## 2. Proposed testing strategy

Ordered by value-per-unit-effort. Each phase stands alone; stopping after any one leaves the suite
better than it found it.

### Phase A — Fake-platform integration tests (highest value)

**Goal:** exercise the real `NearbyConnectionsImplementation` against a scriptable in-memory transport, so the
session/connection/payload interaction is covered in CI on every push.

Build a `FakeTransport` implementing the same contract the platform layers satisfy, then run **both
sides in one process** — a real advertiser session and a real discoverer session wired to each other.

Behaviours to cover (each currently untested):

- Connect → send bytes → receive on peer; ordering preserved under load.
- Accept / reject / timeout, from both roles.
- Disconnect from each side; `ConnectionDropped` raised exactly once.
- Reconnect to the same peer id; no stale `Connection`, no duplicate device rows.
- Payload arriving before any consumer starts (pins the retention guarantee already tested at the
  `NearbyConnection` level, now end-to-end).
- Simultaneous mutual connect — both sides initiate at once.

**Why this is first:** it needs no devices, runs in milliseconds, and covers the exact seam where
the shipped regression lived.

### Phase B — Contract tests over the platform layer

One shared suite, run twice — once per platform implementation — asserting the invariants both
must satisfy. This is where iOS gets its first automated coverage.

Candidates, drawn from behaviour already documented as platform-divergent:

- A payload sent immediately after connect is not lost.
- `ConnectedPeers` lag on iOS does not surface a half-built connection.
- An unclean timeout does not poison the next connect attempt (Android GMS
  `STATUS_ALREADY_CONNECTED_TO_ENDPOINT`).
- Per-peer disconnect leaves other peers connected (iOS `ControlMessage` path).

**Cost, stated honestly:** this needs a device/simulator lab in CI. It is the most expensive phase
and should not be attempted before Phase A proves the seam design.

### Phase C — Stabilize UI tests, then narrow their job

Do not grow this suite. Reduce it to what only it can prove: that a real MAUI app on real hardware
completes an end-to-end connect and transfer. Everything else migrates down to Phase A/B, where it
runs faster and more reliably.

Concretely: keep the full-lifecycle and one transfer test; move disconnect-observation and
payload-delivery assertions down a layer once Phase A covers them.

---

## 3. Connection lifecycle — open items

Gaps 2 (invitation timeout) and 3 (per-peer disconnect) are **closed and shipped**. What follows is
what remains, re-verified against the current source rather than inherited from the old analysis.

### 3.1 `ControlMessage` has no version byte — **fix first**

`ControlMessage.Encode` writes a 4-byte signature plus a 1-byte type. There is no protocol version.

This is already on the wire (iOS disconnect signalling), so the cost is accruing now. Two devices
running different plugin versions have no way to negotiate or refuse. Adding a version byte is a
few lines today and a breaking wire change once anyone has shipped.

**Recommendation:** add it before any further `ControlMessage` work — gap 1 and gap 4 both extend
this protocol, and both get harder if the version byte lands after them.

### 3.2 `ControlMessage` can silently swallow an app payload

`NearbyConnections.ios.cs` tries `ControlMessage.TryDecode` on **every** inbound `NSData` before
treating it as a payload. Any application byte payload that is exactly 5 bytes long and happens to
begin with `PMNC` is consumed as a control message and never delivered.

Improbable, but silent and effectively undebuggable if it ever happens. Worth closing while the
version byte is being added, since both touch the same frame format.

### 3.3 Unconsumed connection is an unbounded memory leak

The receive channel is unbounded and `TryWritePayload` writes unconditionally. That is what makes
late-consumer replay work (verified by test), but it means a connection nobody drains grows without
limit — a peer streaming video into an unconsumed connection will exhaust memory.

Needs a bound plus an explicit drop policy. Note this trades against replay: any bound is a cap on
how much history a late consumer can recover. **This is a design decision, not a bug fix**, and
should be framed as such before implementing.

### 3.4 Gap 1 — failure reason (`EndReason`)

Still open. Design already worked out in `DEVICE-LIFECYCLE.md`: the rejecting peer sends
`ControlMessage.Reject` before tearing down, making `Rejected` certain on both platforms and
leaving only genuine transport failure as `Lost`/`Error`. Depends on 3.1.

### 3.5 Gap 4 — inbound request expiry

Still open. Neither platform expires a pending inbound request, so a request left unanswered holds
the remote peer indefinitely. Entirely plugin-owned, therefore uniform for free: one timer driven by
the injected `TimeProvider`, one `EndReason.Expired`. Testable with `FakeTimeProvider` in Phase A.

### 3.6 Backgrounding — the platforms differ fundamentally ⚠ **confirmed 2026-08-04**

Observed on device: on iOS, when one of two connected devices sleeps, the connection drops entirely.
This is **correct, documented platform behaviour — not a plugin defect.**

**iOS: MPC does not run in the background, at all.** This is categorical, and it comes from Apple
DTS (Quinn "The Eskimo!") on the record — not community inference:

> Multipeer Connectivity does not support operating in the background; it's really that simple. You
> may be able to get some things to work, but I recommend that you not go down that path because,
> when you do unsupported things, you run the risk of future changes breaking your code.
> — [Apple Developer Forums 11964](https://developer.apple.com/forums/thread/11964)

The same engineer adds: *"avoid Multipeer Connectivity, and instead focus your efforts on Network
framework"* — which independently reinforces the already-tracked MPC-deprecation migration.

Mechanics that matter here:

- **Suspension, not backgrounding, is what kills it.** Networking generally survives backgrounding;
  nothing survives suspension. MPC has no background mode that prevents suspension, so a normal app
  is suspended within seconds of backgrounding.
- **Failure is fast and silent** — roughly the first second, with `MCSessionState.NotConnected` and
  **no `NSError`**. That is indistinguishable from a rejection or a peer walking away, which is the
  same overloading already recorded as finding 3 in [`DEVICE-LIFECYCLE.md`](DEVICE-LIFECYCLE.md).
- **Apple's prescribed handling:** observe `UIApplicationDidEnterBackgroundNotification`, call
  `disconnect()` on every `MCSession`, and rebuild from delegate callbacks on foreground return.
  There is no reconnect API — you re-advertise/re-browse and re-invite.
- **`Info.plist` background modes do not help.** Only an app kept unsuspended for an independent
  legitimate reason (audio, VoIP, location) keeps networking alive, and even then MPC specifically
  remains unsupported.

> **Closed 2026-08-04.** `AppLifecycleObserver` (iOS-only) now observes
> `UIApplication.DidEnterBackgroundNotification` and calls `NearbyConnectionsImplementation.StopAsync`. See §3.7 for
> the decisions behind the scope, and §3.6.1 for what was left open.

Previously the plugin did none of this — it had **no app-lifecycle handling whatsoever**. The result
was a zombie session the plugin still reported as `Connected` after iOS had already torn it down.

Separately, since iOS 15 MPC also drops **idle** connections even in the foreground; the community
workaround is a ~1s keepalive ([forum 691072](https://developer.apple.com/forums/thread/691072)).
Different trigger, same "connection silently dies" surface.

**Android: no framework-level prohibition. The constraints are process lifetime *and* power
management — not API policy.**

> There is no restriction on connecting to a device while the app is in the background, although the
> connection is closed if your process is killed.
> — [Android: Communicate in the background](https://developer.android.com/develop/connectivity/bluetooth/ble/background)

Confirmed by a Nearby Connections maintainer: *"There are no restrictions on using Nearby Connections
from a service. However, Android has always somewhat aggressively killed background services (and is
more aggressive since Android Oreo). There's also no way to limit the power, so advertising,
scanning, and maintaining a connection for a long period of time will adversely affect battery
life."*

Google's Nearby docs say nothing about backgrounding at all (checked overview and manage-connections)
— no lifecycle section, no Doze guidance, no reconnect primitive. `onDisconnected` is documented as
terminal; recovery means a fresh `startAdvertising`/`startDiscovery` + `requestConnection`.

**Two constraints, not one:**

1. **Process death** — kills the connection outright.
2. **Doze** — *independently* suspends network access, ignores wake locks, and performs no Wi-Fi
   scans. Doze engages only when the device is stationary, unplugged, and screen-off for a sustained
   period, so it is a long-idle concern rather than a "user pressed home" one.
   ([Doze and App Standby](https://developer.android.com/training/monitoring-device-state/doze-standby))

**Foreground service is the supported mitigation**, and on Android 14+ it has concrete requirements:
`foregroundServiceType="connectedDevice"`, the `FOREGROUND_SERVICE_CONNECTED_DEVICE` permission, and
at least one granted runtime permission among `BLUETOOTH_CONNECT` / `BLUETOOTH_ADVERTISE` /
`BLUETOOTH_SCAN` / `UWB_RANGING`.

**Sample gap (verified):** `samples/NearbyChat/Platforms/Android/AndroidManifest.xml` declares all
the Bluetooth permissions but **neither `FOREGROUND_SERVICE` nor
`FOREGROUND_SERVICE_CONNECTED_DEVICE`**, and wires no service. Demonstrating background survival on
Android therefore needs manifest work, not just code. Note Android's own counter-warning: *"Don't
start a foreground service just to prevent the system from determining that your app is idle."*

**Compounding hazard:** backgrounding-induced process death is precisely the scenario that leaves
GMS believing the endpoint is still connected → `STATUS_ALREADY_CONNECTED_TO_ENDPOINT` on the next
attempt. `PlatformAbandonConnectAsync` exists to clear that, but **cannot run if the process is
killed**. So Android's failure mode is not "connection quietly ends" — it is "connection ends *and*
poisons the next connect." Any reconnect story must handle this explicitly.

**Why this matters more than the Strategy/Topology asymmetry:** that one is visible at the API
surface. This one is invisible — identical code, identical calls, silently different connection
lifetime per platform.

#### 3.6.1 What shipped, and what it deliberately does not do

`AppLifecycleObserver.ios.cs` subscribes to `UIApplication.DidEnterBackgroundNotification` and calls
the session's existing `StopAsync` — no bespoke teardown path. `StopAsync` already stops advertising
and discovery, disposes every connection (so `ConnectionDropped` is raised through the one existing
code path), rejects outstanding inbound requests, and clears `Devices`. The observer is owned by
`NearbyConnectionsImplementation` and disposed before `StopAsync` during session disposal, so a notification arriving
mid-teardown cannot start a concurrent stop.

Decisions worth recording, because each had a defensible alternative:

- **`DidEnterBackground`, not `WillResignActive`.** The latter also fires for transient
  interruptions that never suspend the app — app switcher, control centre, an incoming-call banner.
  Tearing a live connection down for those would be more disruptive than the bug being fixed.
- **The advertiser and browser are stopped too, not just the `MCSession`.** In practice
  `MCNearbyServiceAdvertiser`/`MCNearbyServiceBrowser` often survive suspension as live objects and
  resume scanning on return — observed on device in NearbyChat. That is *observed behaviour, not a
  documented guarantee*, and relying on it is the unsupported-behaviour trap Apple DTS warns about
  in the same thread cited above. Stopping them also keeps `IsAdvertising`/`IsDiscovering` honest:
  nothing scans while suspended, so reporting `true` would be a second zombie state beside the first.
- **Nothing restarts on foreground.** Consistent with the plugin's "nothing starts on its own"
  contract, keeps permission prompts under app control, and there is no MPC reconnect primitive
  anyway. The app calls `StartAdvertisingAsync`/`StartDiscoveringAsync` again.
- **No opt-out option.** The session is dead either way; the only thing an opt-out would buy is the
  right to keep reporting the zombie `Connected` state.

**Known hazard, pinned by test.** `StopAsync` clears `Devices` synchronously, but per-device state
(`Connection`, `Status`) is cleared by `WatchDisconnectAsync`, which runs as a continuation on the
connection's `Disconnected` task. So a device reference held by the caller can briefly still report
`Connected` after `StopAsync` returns. Consumers binding to `Devices` never observe this — the row
is already gone. It is accepted on iOS because the observer *cannot* await teardown (UIKit allows
only seconds before suspension, and blocking risks a watchdog kill), and because iOS has already
destroyed the transport regardless; state is rebuilt from scratch on foreground.
`StopAsync_ClearingDeviceState_IsNotSynchronous` documents this and will fail if it ever needs to
become synchronous.

**Android is untouched.** There is no framework prohibition there (§3.6), so the same teardown would
be a regression, not a fix — it would end connections Android is willing to keep. Android's story is
a foreground service, which is sample/manifest work, not plugin work.

### 3.7 Reconnection — recommendation: do not auto-reconnect

Auto-reconnect should **not** be the plugin's default:

- On iOS it is futile — a backgrounded app cannot hold an MPC session, so retrying accomplishes
  nothing until the app returns to the foreground.
- It fights user intent: a deliberate `DisconnectAsync` must not be undone by a retry loop.
- On Android it risks amplifying the GMS `STATUS_ALREADY_CONNECTED_TO_ENDPOINT` endpoint-poisoning
  problem — and the backgrounding case is exactly where the existing cleanup
  (`PlatformAbandonConnectAsync`) cannot run, because the process is gone. A naive retry loop would
  hammer an endpoint GMS already considers connected. Recovery here needs an explicit
  `DisconnectFromEndpoint` *before* the first retry, not backoff.
- Retry policy (how often, how long, with what backoff, whether to prompt the user) is genuinely
  app-specific.

**What the plugin should own instead:**

1. ~~**Lifecycle-aware teardown on iOS.**~~ **Done 2026-08-04** — see §3.6.1. Explicit, documented
   transition: connections raise `ConnectionDropped`, the toggles go false, nothing restarts silently.
2. **`EndReason`, for user-facing explanation — not for reconnect policy.** This is gap 1 (§3.4).
   The original rationale here ("the input a reconnect policy needs") is **void** under the §0 scope
   decision: auto-reconnect is out of scope, and backgrounding teardown is now an explicit
   transition an app can already observe. The remaining value is real but narrower — telling a user
   *why* a pairing or transfer ended ("they declined" vs. "they went out of range"), which is worth
   having in a transfer/pairing product. It ranks **below** §3.5 request expiry; see §0.
3. **Documented asymmetry + the hooks to build a policy on** — `ConnectionDropped` carrying a
   reason, plus enough device state to re-initiate. The app decides whether and when.

**Open design question — resolved by §0:** an *opt-in* reconnect helper is **not planned**. For
foreground transfer and pairing, a dropped connection means the user backgrounded the app, walked
away, or finished; re-initiating is a deliberate user action, not a policy the plugin should
automate. Revisit only if a concrete consumer scenario demands it.

### 3.8 Other lifecycle questions not yet examined

- **Permission revocation mid-session.**
- **Radio toggled off** (airplane mode, Bluetooth disabled) while connected.
- **Multi-peer.** Everything verified so far is 1:1. `Topology`/`NearbyTopology` permits more, and
  no test covers three or more devices.

---

## 4. Recommended order

1. ~~**3.6 iOS lifecycle teardown**~~ — **done 2026-08-04.** See §3.6.1. Was promoted to first
   because it is a *correctness* bug users hit today (zombie `Connected` state after backgrounding),
   small, and explicitly prescribed by Apple. Everything below is hardening; this one was wrong
   behaviour in the shipped product.
2. **Transfer/pairing sample** — new, and first among the remaining work. Under §0 this is the
   cheapest test of the scope decision itself: if a "send these photos to the device next to you" or
   pairing-handshake sample is straightforward to write and pleasant to use, the scope is right. It
   also replaces NearbyChat as the thing that demonstrates what the library is *for*. Doing this
   before the hardening below means the hardening is driven by a real consumer of the target shape.
3. **3.5 gap 4 (request expiry)** — **promoted above `EndReason`** (see §0). A pairing prompt nobody
   answers is a concrete, common case in the target scope. Self-contained, plugin-owned, uniform
   across platforms, `FakeTimeProvider`-testable. Best done with Phase A, though the timer logic can
   land before it.
4. **3.3 channel bound** — **promoted**: file transfer is the workload that fills an unbounded
   channel, so this is now on the primary path. Still needs the replay trade-off decided first (§5).
5. **Phase A** — the missing middle; highest coverage gain per hour, and the only practical way to
   test lifecycle transitions deterministically.
6. **3.1 version byte** — cheap, and it blocks 3.4. Was second; nothing forces it earlier now that
   `EndReason` has moved down, but it stays cheap whenever it is done.
7. **3.2 frame collision** — small, same code area as 3.1.
8. **3.4 gap 1 (`EndReason`)** — demoted (§0): its reconnect-policy rationale is void. Still worth
   having to explain *why* an interaction ended. Extends the wire protocol, so it follows 3.1.
9. **Phase B / C** — after the above; Phase B is gated on CI device capacity.

---

## 5. Open questions

- **3.3** — what is the right bound, and what should happen on overflow: drop oldest, drop newest,
  or fault the connection? Each is defensible; the choice changes the consumer contract.
- **Phase B** — is a CI device lab available, or should platform-layer coverage stay manual and be
  documented as such rather than planned for?
- **3.8 multi-peer** — is multi-peer a supported scenario for 1.0, or explicitly deferred? The answer
  determines whether Phase A needs an N-way fake transport or a 2-way one is sufficient.
  **§0 narrows but does not settle this:** transfer and pairing are overwhelmingly 1:1, which is an
  argument for a 2-way transport and deferring N-way. But `NearbyTopology` already exposes `Cluster`
  and `Star` publicly on Android, so "deferred" needs to be stated in the docs rather than left
  implied by an untested API.
- ~~**3.6 iOS teardown semantics**~~ — **decided: cleared.** Retaining peers as `Visible` was
  rejected because browsing has stopped, so nothing verifies those rows are still in range or even
  still running — a "reconnect" button built on them would be offering a stale promise. MPC
  re-reports peers via `FoundPeer` once the app restarts discovery. See §3.6.1.
- **3.7 opt-in reconnect helper** — worth offering once `EndReason` lands, or leave entirely to apps?

# Platform-abstraction review

A design review of `src/Plugin.Maui.NearbyConnections/Native/`, prompted by the device-test split
(commit `025dcae`), which forced every shared test into an Android file and an iOS file. The way
those tests had to split tells us something about the code they test. This document says what,
decides what to do about it, and records what was deliberately left alone.

File references are pinned to commit `025dcae` and name the member as well as the line, so they
survive small edits. Nothing in this review changed code — it produced this document, two short
additions to `AGENTS.md`, and the work list below.

## 1. The short version

The two platform implementations differ in three ways that are **forced by the SDKs** and are
fine. They differ in three ways that are **just drift**, one of which is a real (small) bug. And
the device tests turn out to test a contract that exists in practice but was never written down —
so we wrote it down in prose, and deliberately did *not* turn it into a new interface, because the
iOS half is going to be rewritten for Apple's Network.framework and a new interface designed today
would be shaped by the API we're abandoning.

## 2. What needs your input

Nothing right now. One decision was already made here:

**The issue tracker is not the tracking home for this work.** Every GitHub issue (#45–#58 at
least) was closed in one ~11-second sweep on 2026-08-12 with no comments, including issues for
work that plainly isn't done (#45, the Network.framework migration; #56, the ControlMessage
version byte). Decided 2026-08-13: the issues stay closed, and **the work list in §3 is the
tracking home** for the items this review produced. Historical issue numbers below are references,
not live trackers. (If GitHub tracking is revived later, cut issues from §3 at that point.)

Two decisions will come to you later, and are noted here so they don't surprise you:

- When someone picks up the `#if IOS` cleanup (§3, step 4), it needs a short written plan first —
  it touches shared code and both platforms at once, which is this repo's escalation trigger.
- When the Network.framework migration starts, its first design commit should define the internal
  seam described in §4.3. That's the moment to decide its shape — not before.

## 3. Where we're headed

**Status: steps 1–4 are complete as of 2026-08-13.** What remains is step 5 — the items this
review deliberately deferred to the Network.framework migration.

The work list, in order. Each step is small; the order matters.

1. ~~**Run the Android device tests on an emulator once.**~~ **Done 2026-08-13 — 17/17 green**
   (commit `0707bb6`). The suite's 45 tests span both platforms: 17 compile into the Android build,
   28 into the iOS one.
   One fix was needed: DeviceRunners launches `<package>/.MainActivity`, but without an explicit
   `Name` on `[Activity]` the Java binding generator emits `crc64<hash>.MainActivity`, so the
   launch was rejected and the run died at "Starting the application..." with an empty logcat —
   which reads as a hang. The safety net for steps 2–4 now exists.
2. ~~**Fix the cancellation-token bug (PA-2).**~~ **Done 2026-08-13** (commit `06c9a77`).
   `RegisterConnectionTcs` now sits beside the resolve/fault helpers, with
   `AttachConnectionTcsToken` for Android's late-arriving token; all three registration sites route
   through it.
3. ~~**Deduplicate the connection-teardown ritual (PA-3).**~~ **Done 2026-08-13** (commit
   `8b0dc19`). `ReleaseConnection` is the single copy; the iOS-only KVO cleanup hangs off a
   `PlatformReleaseConnection` partial — the platform hook pair, not an `#if`.
4. ~~**Remove the `#if IOS` from shared code (PA-1).**~~ **Done 2026-08-13** (commit `136326c`).
   The constructor is uniform across all three targets; the iOS-only collaborators moved to the iOS
   partial as `required init` properties. The runtime null-check became a compile-time guarantee
   (CS9035), verified by deliberately omitting one. `PlatformNearby.shared.cs` now contains zero
   `#if`.
5. **Everything else waits for the Network.framework migration.** See §5 for what was deliberately
   dropped and why.

## 4. What we found

### 4.1 Differences that are correct

The SDKs genuinely differ, and the code differs where they do. Don't "fix" these:

- **Identity.** Google's SDK identifies a peer by a plain string; Apple's uses an `MCPeerID`
  *object* with no stable string form. That's why iOS needs three extra collaborators
  (`PeerKeyProvider`, `LocalPeerIdentityStore`, a handle-tracking `PeerRegistry` extension) and
  Android needs none. The test factories mirror this: a one-liner on Android, a three-object graph
  on iOS.
- **Connection results.** Google gives a one-shot success/failure callback plus a separate
  disconnect callback. Apple gives a single state-change callback that means "connecting" or
  "connected" or "disconnected" depending on the value. So `ConnectionResultTests` drives different
  mechanisms per platform to check the same promise: *every failure path must resolve or fault the
  pending handshake, or `AcceptAsync`/`ConnectAsync` hang forever.* The tests differing here is
  honest. (The iOS handler's "disconnected" branch does three jobs in ~70 lines — that's real, but
  splitting it is deferred to the migration, which rewrites this file anyway.)
- **Sessions and startup.** Android uses two independent stateless clients; iOS shares one
  `MCSession` guarded by a lock. Google's start call can fail immediately; Apple only ever reports
  failure via a delegate, never success — which is why iOS has a "grace window" before declaring a
  start successful. That window is an already-recorded open question; this review leaves it alone
  (and notes it's MPC-specific — the migration probably deletes it).

One pattern deserves a name because it's the *right* way to do platform divergence and the codebase
already uses it: the **platform hook pair** — shared code declares a `partial void`, exactly one
platform implements it, everywhere else the call compiles away. No `#if`, no stub files. It's now
named in `AGENTS.md` as the sanctioned mechanism. (Its one cost: partial-method signatures must
match on every platform, which is why `CreatePlatformNearby` has a parameter Android ignores —
that's the mechanism's price, not sloppiness.)

### 4.2 Differences that are drift

Three worth fixing — they are steps 2–4 of §3 above (PA-2, PA-3, PA-1). Four more were found and
deliberately dropped; they're listed in §5 so the dropping is a decision, not an omission.

### 4.3 The seam the tests actually test

This is the review's main insight.

The declared platform boundary is `IPlatformNearby`: five methods, consumed by exactly one class.
Clean. But the device tests barely touch it — 3 direct calls, versus **114** touches on things the
interface never mentions: the internal callbacks the SDKs invoke, and the internal channels, the
handshake map, and the peer registry those callbacks feed. Three of the interface's five methods
have *zero* device-test coverage, because they're the ones that would start a real radio.

In other words: there are two contracts. The interface is the contract for *consuming* a platform.
The tests exercise the contract for *implementing* one — "SDK callbacks in, channel/registry
effects out." That second contract was real but unwritten. It's now written — in prose, in
`AGENTS.md` — under the name **the platform event surface**, and the device tests are described as
its executable specification.

Should it become an actual type — an interface or event-sink the tests and SDKs both drive?
**Not yet, on purpose.** An abstraction designed today would be molded around MultipeerConnectivity's
callbacks, and MPC is scheduled for replacement; we'd design it twice. It would also mean
rewriting the entire device suite before its Android half has ever run. The trigger for revisiting:
*the first design commit of the Network.framework migration defines this seam as a type, shaped by
what a second real backend actually needs. If the migration is ever abandoned, revisit on Android's
needs alone.*

## 5. Dropped findings and open questions

Dropped — found, considered, and rejected because they only polish iOS internals the migration
deletes (repo rule: don't churn internals for symmetry):

- Callback wiring style differs between platforms (delegate lists vs `this`-references). Cosmetic.
- Three different construction idioms across the four iOS identity collaborators. The actionable
  sliver (uniform null-checks) folds into step 4.
- `s_nextPayloadId` is `static` on iOS for no reason. One-token fix; ride along on some future
  iOS commit, never scheduled alone.
- `PeerRegistry` logs under `PlatformNearby`'s category. Cosmetic; same treatment.

Open questions this review touches but does not settle (each already has a home):

- The iOS start-failure grace window (`DESIGN-PRINCIPLES.md`).
- The `net10.0` target's inability to enumerate streams, and what "platform-unsupported" means
  (`DEVICE-LIFECYCLE.md`).
- Renaming `InvitationTimeout`; lifecycle gaps 1 & 4; the package rename.
- New, from this review: whether the iOS state-change handler's three-jobs-in-one-branch gets
  split during the migration. (The issue-tracker question was open here briefly and is now settled
  — see §2.)

## 6. Evidence

Seam counts, reproducible at `025dcae` from `test/Plugin.Maui.NearbyConnections.DeviceTests/`:

```bash
# touches on internal fields the interface doesn't declare        → 47
grep -rc '_connectionTcs\|_advertiseChannel\|_discoverChannel\|_activeConnections' \
  --include='*.cs' . | awk -F: '{s+=$2} END {print s}'
# touches on the internal peer registry                           → 23
grep -rc 'platform\.Peers' --include='*.cs' . | awk -F: '{s+=$2} END {print s}'
# direct invocations of internal SDK callbacks                    → 44
grep -rhoE 'platform\.(On[A-Za-z]+|FoundPeer|LostPeer|DidReceiveInvitationFromPeer|DidNotStart[A-Za-z]+)\(' \
  --include='*.cs' . | wc -l
# calls to IPlatformNearby's declared members                     → 3
grep -rhoE 'platform\.(AdvertiseAsync|DiscoverAsync|ConnectAsync|CheckAvailabilityAsync|DisposeAsync)\(' \
  --include='*.cs' . | wc -l
```

Key code locations (member names are the stable reference; lines are as of `025dcae`):
the `#if IOS` blocks — `PlatformNearby.shared.cs:36-39,51-55,65-73` (fields + constructor);
token divergence — `OnConnectionInitiatedAsync`, `android.cs:70` vs the accept path,
`ios.cs:122`, settled by `DisposeAsync`, `shared.cs:246-248`; the four teardown copies —
`android.cs:172-177,503-508`, `ios.cs:513-519,528-537`; the shared helper family —
`PlatformNearby.events.cs` (`WriteDeviceFound/Lost`, `WriteConnectionRequest`,
`ResolveConnectionTcs`, `FaultConnectionTcs`, `WritePayload`); the iOS state handler —
`OnPeerStateChanged`, `ios.cs:478-605`.

Every claim above was verified against the pinned commit at writing time; the counts were produced
by the commands shown. This review changed no code.

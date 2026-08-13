# Platform-abstraction review — what the device-test split exposed

Pinned at commit `025dcae` (`test: split device tests per platform and repair AAA structure`).
Every `file:line` reference below resolves against that commit; where a later edit may move a line,
the reference also names the member. The deliverable of this review is this document and its
follow-up table (§7). No code changed.

Finding IDs are `PA-<n>` and are meant to be cited from other docs and commits. (A prior review is
cited twice in `DEVICE-LIFECYCLE.md` as "review finding P2-1", but P2-1 is defined nowhere in the
repo — the reference survived, the review didn't. This document exists in-repo so its IDs resolve.)

## 1. The question, and how to read this

The device-test suite was split into `*.android.cs`/`*.ios.cs` pairs. The shape of that split is
evidence about `src/Plugin.Maui.NearbyConnections/Native/`: every place a test had to know
platform-specific *mechanics* rather than the model-level *claim* it was testing is a place the
abstraction made it know. The question: what does that say about how platform behavior maps onto
the plugin's model layers, and would restructuring — perhaps behind interfaces — make the tests'
intent clearer?

| I want to know… | Read |
|---|---|
| What the plugin *declares* as its platform boundary | §2 |
| Which asymmetries are SDK-forced and correct | §3 |
| Which asymmetries are drift | §4 |
| What seam the tests actually exercise | §5 |
| Why some findings get no action | §6 |
| What happens next | §7 |

Two lenses are applied throughout, both binding:

- **The Network.framework migration.** Apple deprecated MultipeerConnectivity; the iOS half will be
  rewritten (`DESIGN-PRINCIPLES.md` — "Vendor-neutral names survive that migration unchanged").
  Every recommendation is tested against: does it survive the swap, ease the swap, or polish what
  the swap deletes? The third kind is killed.
- **The anti-churn rule.** `naming.md` — "Internal names need not be branded… Do not churn internals
  for aesthetic symmetry." An abstraction with one caller-shape, introduced for symmetry, is
  prohibited by the repo's own rules.

## 2. The abstraction as declared

`IPlatformNearby` (`Native/IPlatformNearby.cs`) declares exactly five members:
`AdvertiseAsync(TaskCompletionSource, CancellationToken)`, `DiscoverAsync(…)`,
`ConnectAsync(NearbyDevice, CancellationToken)`, `CheckAvailabilityAsync(CancellationToken)`, and
`DisposeAsync()` via `IAsyncDisposable`. No properties, no events, no streams-as-members.

`NearbyImplementation` consumes exactly those five and nothing else. The interface exists so the
`net10.0` target — whose `Platform*` methods throw `PlatformNotSupportedException` by design — can
be faked off-device (`AGENTS.md`, Architecture). The single implementation is one
`sealed partial class` across `Native/PlatformNearby.{shared,android,ios,net}.cs`.

The production seam is honest and narrow. Hold that thought for §5, where a second seam appears
that is neither declared nor narrow.

## 3. Essential asymmetries — correctly different

SDK-forced differences. Each entry: what differs / why the SDK forces it / what the test split made
visible. These are documented as correct; changing them is not on any follow-up list.

**3.1 Identity: object vs string.** GMS hands every callback a `string endpointId`; MPC identity is
an `MCPeerID` *object* with no stable string form. Hence the iOS-only identity cluster:
`PeerKeyProvider` (key = hex of truncated SHA-256 over `NSKeyedArchiver` bytes),
`LocalPeerIdentityStore` (memoizes the local `MCPeerID` against a real GC/toggle-ref interop
hazard), and `PeerRegistry.ios.cs`'s handle dictionary (the shared registry would otherwise hold a
copy of its own key on Android — the second-dictionary shape is deliberately pay-per-platform).
The test-visible symptom: `Create.PlatformNearby()` is a 1-line pass-through on Android and a
3-object graph on iOS; `Create.PendingHandshake` needs two signatures. That is the SDK difference
showing through, not a defect.

**3.2 Connection result: a callback vs a state change.** Android has a discrete
`OnConnectionResult(endpointId, resolution)` (success/failure, once per handshake) and a separate
`OnDisconnected`. MPC has neither — one `OnPeerStateChanged(MCPeerID, MCSessionState)`
(`PlatformNearby.ios.cs:478-605` at `025dcae`) carries connecting, connected, and not-connected.
This is *essential in origin, accidental in shape*: the SDK forces the single callback, but the
plugin adopted MPC's shape wholesale, so the `NotConnected` branch (~70 lines) mixes three
responsibilities — teardown of an established connection, faulting a pending handshake, and
session-lifetime refcounting — with a 30-line comment (ios.cs:550-568) carrying the disposal
heuristic. `DEVICE-LIFECYCLE.md`'s "single most important constraint" (NotConnected is overloaded
and carries no `NSError`) already documents the *why*; this review adds the *shape cost*. Whether
to split the branch is deliberately deferred to the migration (§8).

The test-visible symptom is the sharpest of all: `ConnectionResultTests.android.cs` and
`ConnectionResultTests.ios.cs` drive *different concepts* (a result callback vs a state transition)
to verify *the same model claim* — "every failure path must resolve or fault the pending
`_connectionTcs` entry". The tests are correct to differ; the claim they share is the contract.

**3.3 Session model: two stateless clients vs one shared session.** Android holds independent
`_advertiseClient`/`_discoverClient`, separately nulled; iOS holds one `_session` shared by
advertising and discovery, guarded by `_sessionLock`, with refcount-style teardown. GMS's
`ConnectionsClient` is stateless-per-call; `MCSession` is shared mutable state. Essential.

**3.4 Start failure: awaitable vs delegate-only.** GMS start calls are awaitable and throw; MPC
reports *only* failure (`DidNotStartAdvertisingPeer`/`DidNotStartBrowsingForPeers`), never success
— hence the iOS-only direct channel faulting (`_advertiseChannel.Writer.TryComplete(exception)`,
ios.cs:70,82) and the `StartFailureGraceWindow`. The grace window is an **open question**
(`DESIGN-PRINCIPLES.md`, Deliberately undecided) and is *raised here, not settled*. Under the
migration lens it is also a pure MPC workaround that Network.framework likely deletes — a reason
not to invest in refining it.

**3.5 The model mechanism: the platform hook pair.** `NearbyImplementation` declares
`partial void PlatformInitializeLifecycleObserver(ILogger)` / `PlatformDisposeLifecycleObserver()`;
exactly one platform file (`NearbyImplementation.ios.cs`) implements them; on Android the calls
compile to nothing. No `#if`, no empty stub file, no unused parameter. This is the cleanest
asymmetry mechanism in the codebase and is now named in `AGENTS.md` as the sanctioned pattern
(edit D1, landed with this review).

**3.6 The mechanism's cost is not drift.** `CreatePlatformNearby` (partial method in
`ServiceCollectionExtensions.cs:94-107`) has an `IServiceProvider` parameter that Android and net
ignore, and `.android.cs`/`.net.cs` bodies that are byte-identical. Partial-method signatures must
match across all platform files and each TFM compiles its own body — this is the *forced cost* of
the pattern §3.5 praises, not organizational drift. It was initially classified as drift during
this review and reclassified on verification. No action beyond this paragraph.

## 4. Accidental asymmetries — drift

Differences with no SDK justification. Every row receives a verdict in §7 — including "killed", so
a dropped finding is visibly decided rather than silently omitted.

| ID | Symptom | Where (at `025dcae`) | Why drift, not essence |
|---|---|---|---|
| PA-1 | `#if IOS` in shared code: ctor params, fields | `PlatformNearby.shared.cs:36-39,51-55,65-73` | Violates `AGENTS.md` ("Platform code lives in platform partials, never `#if` in shared logic"). The codebase argues against itself: `ServiceCollectionExtensions.cs:98-102` chose a partial method over an inline `#if` *"so the platform/shared boundary this codebase keeps checkable via file suffix extends to this registration code too"* — the ctor that partial method calls is where the rule is broken. |
| PA-2 | TCS registration unabstracted; token divergence | `android.cs:70` stores `CancellationToken.None`; `ios.cs:122` stores caller's `ct`; `events.cs:60,76` | The completion side of the handshake lifecycle has shared helpers (`ResolveConnectionTcs`/`FaultConnectionTcs`); the registration side has none, and the two hand-rolled registrations have already diverged. See below. |
| PA-3 | Connection-teardown ritual duplicated ×4 | `android.cs:172-177,503-508`; `ios.cs:513-519,528-537` | `TryRemove` → `CompleteReceive()` → `_unobservedWarned.TryRemove` (+`RemoveProgressObserversFor` on iOS), four near-identical copies, no helper. |
| PA-4 | Callback wiring style differs | `android.cs:736-791` (delegate lists, repeated at 2 construction sites) vs `ios.cs:776-820` (delegates hold `this`) | Both forward identically; the difference is stylistic. |
| PA-5 | Three construction patterns across four collaborators | ctor+null-check (`PeerKeyProvider`, `LocalPeerIdentityStore`); `required init` (`PeerRegistry`); plain `init` never used as init and un-null-checked (`PlatformNearby.shared.cs:37-39`; only `peers` is checked, at :66) | One folder, four types, three idioms. |
| PA-6 | `s_nextPayloadId` is `static` | `ios.cs:7` | Process-wide for no reason; ids are synthetic and per-instance would do. Harmless today. |
| PA-7 | `PeerRegistry.Logger` receives `ILogger<PlatformNearby>` | `PeerRegistry.ios.cs:26`; DI site `ServiceCollectionExtensions.ios.cs:15` | Registry trace logs are categorized under `PlatformNearby`. Cosmetic. |

**PA-2 deserves its full statement, because it is the repo's stated dominant defect class — "a fix
applied to one platform partial and not its sibling" — caught live.** `DisposeAsync`
(`shared.cs:246-248`) settles every pending handshake with `entry.Tcs.TrySetCanceled(entry.Ct)`.
iOS stored the caller's token, so an `AcceptAsync` awaiter can correlate the resulting
`OperationCanceledException` to its token. Android stored `CancellationToken.None`, so the same
cancellation carries no provenance. The nuance a fix must respect: the platforms register at
*different lifecycle moments* — Android at request-surfaced time, inside the GMS callback, where no
caller token exists yet (`android.cs:68-70`); iOS at accept-called time, token in hand
(`ios.cs:118-122`). The comment at `android.cs:84-88` shows the author met this exact
misattribution and fixed the *removal* path — the *registration* still stores `None`. A
`RegisterConnectionTcs` helper closes the asymmetry and the Android fix is to update the entry with
the real token inside the accept lambda; the mechanics belong to follow-up PA-2, not to this doc.

## 5. The undeclared second seam

**The numbers.** At `025dcae`, in `test/Plugin.Maui.NearbyConnections.DeviceTests/`:

```bash
# internal-field touches (not on IPlatformNearby)
grep -rc '_connectionTcs\|_advertiseChannel\|_discoverChannel\|_activeConnections' --include='*.cs' . \
  | awk -F: '{s+=$2} END {print s}'                                                                       # 47
grep -rc 'platform\.Peers' --include='*.cs' . | awk -F: '{s+=$2} END {print s}'                           # 23
# direct callback invocations (not on IPlatformNearby)
grep -rhoE 'platform\.(On[A-Za-z]+|FoundPeer|LostPeer|DidReceiveInvitationFromPeer|DidNotStart[A-Za-z]+)\(' --include='*.cs' . | wc -l   # 44
# interface-member calls
grep -rhoE 'platform\.(AdvertiseAsync|DiscoverAsync|ConnectAsync|CheckAvailabilityAsync|DisposeAsync)\(' --include='*.cs' . | wc -l      # 3
```

**114 touches on members `IPlatformNearby` does not declare, against 3 explicit calls to members it
does** (plus ~40 implicit `DisposeAsync` calls via `await using`, which do exercise disposal well).
Three of the interface's five members — `AdvertiseAsync`, `DiscoverAsync`, `ConnectAsync` — have
**zero** device-test coverage, because they are precisely the members that start a live
radio/session. Not one test constructs the type *as* `IPlatformNearby`; both `Create.PlatformNearby`
factories return the concrete class. And the four internal fields are `internal` rather than
`private` *solely* so the tests can reach them via `InternalsVisibleTo`.

**What this means.** The device tests do not test the declared seam. They test a second surface:
**SDK callbacks in → channel/TCS/registry effects out**. Call it *the platform event surface*. Its
inbound edge is the `internal` callback family (`OnConnectionResult`, `OnPeerStateChanged`,
`OnDataReceived`, …) that both the real SDK delegates and the tests drive; its outbound edge is
`_advertiseChannel`/`_discoverChannel`/`_connectionTcs`/`_activeConnections`/`Peers`. This is
sanctioned (`AGENTS.md`: internals of internal types are fair game) but nowhere declared — the
tests pin the implementation, and it happens that the implementation *is* the contract.

This surface is also exactly the boundary a Network.framework implementation re-implements against:
the new backend must feed the same channels, resolve the same TCS map, and keep the same registry
honest. The tests, as restructured, are the executable specification of that obligation.

**Three options, weighed:**

(a) *Introduce an internal event-sink type now* — a named object the callbacks write to, which
tests construct directly. Pro: the 114 touch points become contract exercises; the migration codes
against a named boundary. Con, decisive: the sink's shape today would be derived from MPC's
callbacks, so the migration would reshape the abstraction too — buying a second migration, not
stability. It also retrofits the entire device suite (whose Android leg has never run on-device)
simultaneously with the code under test, and it is precisely the one-caller-shape abstraction the
anti-churn rule prohibits.

(b) *Leave it undeclared* — status quo. Con: the next contributor reads 114 internals touches as
test opportunism rather than a contract, and the migration has no stated obligation to satisfy.

(c) **Declare the seam in prose now; introduce no type until the migration.** The tests already
*are* the specification; what is missing is the sentence saying so. `AGENTS.md` edit D2 (landed
with this review) declares the platform event surface a deliberate second contract and points here.

**Verdict: (c).** Reversal trigger, `DECISIONS.md`-style: *when the Network.framework migration's
first design commit lands, it defines the sink boundary as a type, shaped by what the second
backend actually needs; if the migration is abandoned, option (a) is re-evaluated on Android's
needs alone.* Until one of those happens, introducing the type is churn.

## 6. The Network.framework lens — what was killed and why

The kill rule: a finding whose fix only polishes MPC internals that the migration deletes is not
scheduled, however tidy the fix.

- **PA-4 (wiring style)** — killed. Aesthetic symmetry; the iOS side of it dies with MPC.
- **PA-5 (construction patterns)** — killed as a standalone item. Three of the four types are MPC
  identity machinery the migration reshapes. The actionable remainder — uniform null-checks in the
  shared ctor — folds into PA-1's commit.
- **PA-6 (`s_nextPayloadId`)** — ride-along only. One-token fix on the next `ios.cs` commit; never
  scheduled alone.
- **PA-7 (logger category)** — ride-along only. Cosmetic categorization on migration-doomed code.

What the migration *keeps* — `PlatformNearby.shared.cs`, `events.cs`, the Android partial, and
every model layer above `Native/` — is why PA-1, PA-2, and PA-3 survive the lens: each one moves
logic *into* the kept set (shared helpers, suffix-checkable files) so the migration edits one
platform file instead of shared code.

## 7. Follow-up

| ID | Item | Size | Class | Gate |
|---|---|---|---|---|
| F0 | Android device-test leg green on an emulator. It has never run on-device and is the regression net for every row below. | M | needs-own-plan (infra) | none — hard gate for all code items |
| PA-2 | `RegisterConnectionTcs(peerId, ct)` in `events.cs` pairing Resolve/Fault; Android updated to store the caller's real token. **A behavior fix, not just dedup — the commit must say so.** | S | mechanical-safe | F0 |
| PA-3 | Extract the ×4 teardown ritual into one internal helper. The future iOS backend inherits the helper instead of re-deriving the ritual. | S | mechanical-safe | F0 |
| PA-1 | Remove `#if IOS` from the shared ctor/fields via the repo's own partial pattern; unify null-check policy while there (folds PA-5 remainder). | S/M | needs-own-short-plan — touches shared + both partials at once, the `CLAUDE.md` escalation trigger | F0; sequenced last so the migration edits one platform file, not shared code |
| D1 | `AGENTS.md`: name the platform hook pair (§3.5). | XS | docs-only | landed with this review |
| D2 | `AGENTS.md`: declare the platform event surface (§5). | XS | docs-only | landed with this review |
| PA-4, PA-5 (rest), PA-6, PA-7 | Killed / ride-along (§6). | — | — | never scheduled alone |

Net: eight candidate refactors in; three code items out, one of which fixes a live divergence; two
doc edits; four killed, each visibly.

## 8. Open questions — raised, not settled

Standing rule: these have homes; this review cites them and adds two of its own. None is resolved
here.

1. **`StartFailureGraceWindow`** — `DESIGN-PRINCIPLES.md`, Deliberately undecided. §3.4 adds only
   the migration-lens observation that it is MPC-specific.
2. **The `net10.0` stream-enumeration gap** — `DEVICE-LIFECYCLE.md` ("Closing it… is a change to
   what platform-unsupported *means*, not a test refactor"). §5's seam analysis is adjacent to it
   but does not touch it.
3. **`InvitationTimeout` → `ConnectionRequestTimeout`** — deferred to the single vocabulary pass.
4. **Lifecycle gaps 1 & 4** (uniform failure reason; inbound request expiry) — `DEVICE-LIFECYCLE.md`.
5. **Package rename** — gated by the rename guard.
6. *(new)* **Tracking home**: the GitHub issues formerly holding 1-5 (#45, #52, #53 among them)
   were bulk-closed on 2026-08-12 with no closing comments while their work is visibly not done.
   Either reopen them or adopt this document's §7 table as the tracking home — one or the other,
   decided explicitly.
7. *(new)* **`OnPeerStateChanged` shape** — should the `NotConnected` branch be split into its
   three responsibilities *during* the Network.framework migration rather than before? This review
   deliberately defers it (§3.2): pre-migration splitting reshapes code the migration deletes.

## 9. How this review was verified

All `file:line` references were re-verified against `025dcae` at writing time (concurrent
working-tree edits to `ios.cs` shift post-480 line numbers by −2; references here are to the
pinned commit, and each names its member). The §5 counts were produced by the commands shown
there, run at `025dcae`. Open-question audit: every §8 entry is a question with a home, none
carries a resolution. Follow-up audit: every §4 row has a §7 verdict. No code changed; no build
impact.

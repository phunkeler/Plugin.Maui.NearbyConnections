# Architecture review — DI, SOLID, and structure

Date: 2026-08-24. Scope: the whole repository, weighted to `src/Plugin.Maui.NearbyConnections/`
and `test/Plugin.Maui.NearbyConnections.UnitTests/TestSupport/`. This review reports. It changes
no code. At the maintainer's request, the review re-examined every documented decision from
scratch. It treated nothing as settled.

Confidence labels:

- **confirmed** — the cited code proves the claim by inspection.
- **needs-failing-test** — the mechanism is visible in code, but no test reproduced the
  consequence yet.

---

## 1. Verdict

The smell is real, but it is localized. The macro-architecture is stronger than most shipped
plugins:

- One public seam.
- An injected `TimeProvider` everywhere.
- No service locator, and no ambient state above `Native/`.
- Channel-backed delivery with documented thread contracts.
- A build-enforced public surface whose three baselines are byte-identical.

All ten mechanical checks in `.claude/rules/naming.md` §10 pass.

The problems concentrate in four places:

1. **The two platform partials outgrew the shared-helper discipline.** Six copy-paste slips now
   sit between `Native/PlatformNearby.android.cs` (675 code lines) and
   `Native/PlatformNearby.ios.cs` (638). One slip is behavioral, not cosmetic. The repository
   names this its dominant defect class. The class grows.
2. **Connection bookkeeping is duplicated across layers.** Two `_activeConnections` dictionaries
   hold the same fact under two owners. Time windows exist where the two disagree.
3. **`NearbyImplementation` accretes policy.** Three self-contained machines live inside the
   facade: request expiry, auto-accept, and the discovery refresh loop.
4. **The written contracts decayed.** The code and `AGENTS.md` name two documents as
   authorities — `docs/CONCURRENCY.md` and `docs/PLATFORM-ABSTRACTION-REVIEW.md`. Neither
   document exists. The options document contradicts the code it describes.

No finding blocks correct operation today. Every finding costs more to fix after 1.0.

---

## 2. Findings

Ranked by value. Each finding names the fact, the consequence, and a direction. Directions are
not implementations.

### F-1. The concurrency contract is a broken link — **confirmed**

`docs/CONCURRENCY.md` does not exist. It never existed in git history. Four places cite it as
the authority:

- `AGENTS.md` (three references) — including the instruction to read it "before adding another
  `_ = SomeAsync(...)`" and the claim that it "lists the four drain sites".
- `src/Plugin.Maui.NearbyConnections/Native/KeyedSerialQueue.cs:42` — the doc comment on the
  drain mechanism itself.

Commit 528b9e0 ("Removed outdated ai docs") deleted `docs/PLATFORM-ABSTRACTION-REVIEW.md`.
`DESIGN-PRINCIPLES.md` (lines 15, 18, 75) and `docs/DEVICE-LIFECYCLE.md:12` still name that
document as the live work list.

**Why it matters.** "Drain, then release" is deliberately a prose rule, not a type
(`AGENTS.md` → *Drain, then release*). A prose rule whose prose does not exist is not a rule.
The five fire-and-forget sites in the codebase (F-6) have no written account of who awaits them.
The project retired its issue tracker in favor of the work-list document. The document is gone.
The project now has no record of outstanding work.

**Direction.** Write `docs/CONCURRENCY.md`, or delete the four references and move the
drain-site inventory into `AGENTS.md`. Restore `docs/PLATFORM-ABSTRACTION-REVIEW.md` from git
history, or point the three `DESIGN-PRINCIPLES.md` rows at the current work-list location.

### F-2. A cancelled `ConnectAsync` abandons nothing on the platform — **needs-failing-test**

`AwaitHandshakeAsync` (`Native/PlatformNearby.shared.cs:152-193`) has two failure exits. The
deadline exit removes the pending TCS **and** calls `PlatformAbandonConnectAsync`. On Android
that call disconnects the endpoint and releases the connection
(`Native/PlatformNearby.android.cs:480-490`). The catch-all exit removes the TCS and rethrows
(`shared.cs:189-193`). That exit covers the caller's own token and every other fault. It never
calls the abandon path.

Two consequences:

- On Android, a cancelled connect leaves the GMS handshake active. If the remote device accepts,
  `OnConnectionResult` builds a `NearbyConnection`, finds no TCS entry, and drops it
  (`Native/PlatformNearby.events.cs:123-137`). The endpoint stays connected at the GMS level
  with no local teardown. The remote device believes the connection succeeded.
- If the resolve wins the race instead — `ResolveConnectionTcs` runs before the code observes
  the cancellation — the connection lands in the platform `_activeConnections` (`events.cs:129`).
  The session layer never records it, because `ConnectAsync` threw. The device row resets to
  `Visible` while a live platform connection exists underneath it.

The internal contract documents part of this: "Cancellation removes the pending connection
attempt but does not guarantee the remote device was notified"
(`Native/IPlatformNearby.cs:124-126`). It does not document the orphaned-connection race. The
asymmetry with the deadline path reads as an oversight, not a choice: the deadline exit cleans
up, and the cancel exit does not.

**Confirming test.** A device test. Register a handshake. Cancel the caller token. Drive
`OnConnectionResult` with a success resolution. Assert that the platform `_activeConnections`
does not retain the connection. Assert that the endpoint is no longer connected.

**Direction.** Run the same abandon-and-release path on the catch-all exit. Or state in the
public `ConnectAsync` docs that cancellation can leave the remote side connected.

### F-3. Two `_activeConnections` dictionaries own the same fact — **confirmed** (divergence windows: needs-failing-test)

The session holds one dictionary (`NearbyImplementation.cs:21-22`). The platform holds another
(`Native/PlatformNearby.shared.cs:26`). Both use the device id as key and the `NearbyConnection`
as value. Each side writes on its own (`NearbyImplementation.state.cs:220` vs `events.cs:129`).
Each side removes on its own: the disconnect watcher (`state.cs:232`) and the release path
(`events.cs:187`). Both sides answer the same question — `TryGetConnection` reads the session
copy (`NearbyImplementation.cs:363`), and `WritePayload` reads the platform copy
(`events.cs:213`).

Known divergence windows:

- F-2's race: the platform copy holds a connection the session copy never learns about.
- `StopAsync` disposes each connection and clears the registry. The session copy empties only
  when each `WatchDisconnectAsync` wakes on the thread pool (`state.cs:226-247`).
  `TryGetConnection` can hand out a disposed connection after `StopAsync` returns.

**Why it matters.** A single fact with two owners produces state bugs that appear only under
specific timing. Every future path that touches connections must update both sides. F-2 shows
one path that already skips one side.

**Direction.** One owner. The platform layer is the natural owner: it creates the connections
and already keys them for payload routing. The session then queries through `IPlatformNearby`
(add `TryGetConnection` to the internal seam). Or the session subscribes to a connection-ended
stream instead of one watcher task per connection.

### F-4. Six duplication slips between the platform partials, one behavioral — **confirmed**

The repository's stated defense against its dominant defect class is shared helpers
(`AwaitHandshakeAsync`, `Step<T>`, `ReleaseConnectionAsync`, the staging trio). The discipline
held in those four helpers. It slipped in six places:

| # | Android | iOS | What slipped |
|---|---|---|---|
| a | `OnEndpointLost` (`PlatformNearby.android.cs:208-236`) | `LostPeer` (`PlatformNearby.ios.cs:218-246`) | 25 near-identical lines. Only the id-lookup call differs. |
| b | `OnEndpointFound` (`android.cs:194-206`) | `FoundPeer` (`ios.cs:202-216`) | Same shape, 14 lines each. |
| c | `PlatformSendFileAsync` catch ladder (`android.cs:599-617`) | same (`ios.cs:411-428`) | Identical structure. The third filter differs — see below. |
| d | `Report` local function (`android.cs:579-588`) | `Report` (`ios.cs:388-399`) | The comments claim the two mirror each other. They do not. The divergence has a platform reason, but the iOS side does not document it. |
| e | Unobserved-fault retirement (`android.cs:629`) | (`ios.cs:432`) | Identical load-bearing line. Android carries the four-line explanation. iOS carries nothing. |
| f | "not currently visible" fault (`android.cs:426-428`) | (`ios.cs:257-258`) | Byte-identical exception string, maintained in two files. |

**The behavioral one (c).** Android's third catch filters
`ex is not OperationCanceledException and not NearbyException`. iOS filters only
`ex is not NearbyException`. Consider an `OperationCanceledException` from a source other than
the caller's token or the inactivity token — a foreign linked token, or a cancelled inner await.
Android propagates it raw. iOS wraps it in `NearbyTransferException`. A consumer that catches by
type sees a different exception per platform for the same event. The window is narrow. It is
still the class of leak the first principle exists to prevent.

**Direction.**

- Fold (a), (b), and (f) into shared helpers. Pass the platform-varying lookup as a delegate —
  the same move `AwaitHandshakeAsync` already made.
- Align the filters in (c) deliberately. Add the device test that pins the choice.
- Add the iOS-side comments for (d) and (e), or hoist the shared shape.
- R-5 would absorb all six at once.

### F-5. The options object is mutable after validation, and its doc says otherwise — **confirmed**

`Options/NearbyOptions.cs:10-12` states the library "reads the resolved instance once, when the
session is created — changing a property afterward has no defined effect." Neither half is true:

- `DisplayName` and `ServiceId` are `{ get; set; }` (`NearbyOptions.cs:46,78`), and the code
  reads them at **use** time. Android reads both on every advertising start
  (`Native/PlatformNearby.android.cs:22-23`). iOS reads `ServiceId` per start (`ios.cs:45`) but
  captures `DisplayName` at first peer creation (`ios.cs:30`). A post-registration mutation
  takes full effect on Android and partial effect on iOS. Even the staleness diverges by
  platform.
- Validation runs exactly once, inside `AddNearby` (`ServiceCollectionExtensions.cs:91`). A
  consumer that mutates `ServiceId` after registration bypasses validation completely. On iOS
  the invalid service type then reaches `MCNearbyServiceAdvertiser` and raises the unmanaged
  `NSInvalidArgumentException` the validator exists to prevent
  (`Options/NearbyOptions.ios.cs:24-37` documents that failure mode).

The eager-validation design is sound (see R-1). The hole: the validated object stays shared,
mutable, and captured in the factory closure (`ServiceCollectionExtensions.cs:89-102`) for the
life of the process.

**Direction.** Snapshot at the boundary: after validation, `AddNearby` clones the configured
instance (or copies it into an internal immutable record), and the closure captures the copy.
The doc sentence then becomes true. The 2026-07 DX audit tried and rejected an `init`-only
public surface. The copy gives the same guarantee without that surface change.

### F-6. Five fire-and-forget sites, and no document that owns them — **confirmed**

- `_ = AutoAcceptAsync(...)` — `NearbyImplementation.state.cs:117`
- `_ = ExpireRequestAfterAsync(...)` — `state.cs:143`
- `_ = WatchDisconnectAsync(...)` — `state.cs:223`
- `_ = WatchAsync(changes)` from a constructor — `Devices/NearbyDeviceCollection.cs:128`
- `_ = CancelPayloadLoggedAsync()` inside a token-registration callback —
  `Native/PlatformNearby.android.cs:596`

Each site catches and logs, which satisfies the convention's text. But `AGENTS.md` states
plainly that neither termination guarantee covers this category, and it defers the accounting to
`docs/CONCURRENCY.md` — which does not exist (F-1). Nothing awaits `AutoAcceptAsync` or
`WatchDisconnectAsync` at disposal: `DisposeAsync` cancels `_disposing` and proceeds, so an
auto-accept mid-handshake races teardown. Whether that race has a consumer-visible consequence
is unproven. The missing document was supposed to hold that audit.

**Direction.** Write the inventory (F-1). Separately, consider a set that tracks the
session-owned tasks (`AutoAcceptAsync`, `WatchDisconnectAsync`), which `DisposeAsync` awaits
with a bound — the same shape `KeyedSerialQueue` already gives the platform layer.

### F-7. The rationale for the static `StagingDirectory` only justifies `partial` — **confirmed**

`Native/PlatformNearby.events.cs:20` declares
`internal static partial string StagingDirectory { get; }`. The documented reason
(`events.cs:17-19`) is that `FileSystem.CacheDirectory` does not resolve on `net10.0`. That
reason justifies the *partial* — a per-platform implementation. It does not justify the
*static*. A partial instance property compiles identically on every TFM.

The static keyword makes the path process-wide. The process-wide path forced the device suite to
serialize completely (`test/Plugin.Maui.NearbyConnections.DeviceTests/AssemblyMarker.cs:1-7`):
every `DisposeAsync` sweeps the shared directory and deletes what another test staged.
`s_nextPayloadId` (`Native/PlatformNearby.ios.cs:5`) is the same pattern, smaller. It is the
only mutable static field in src, shared across instances for no stated reason.

**Direction.** Make `StagingDirectory` an instance partial property. Consider a per-instance
subdirectory (`nearby-received/{instance-token}`), so two `PlatformNearby` instances do not
sweep each other's files. That removes the root cause behind the device suite's forced
serialization, not the symptom. Make `s_nextPayloadId` an instance field. Both changes follow
the standing direction to minimize statics.

### F-8. Two `null!` without the required comment — **confirmed**

`Native/PlatformNearby.ios.cs:128` and `ios.cs:267` pass `identity: null!` to the `MCSession`
constructor. The repository convention (`AGENTS.md` → Conventions) is "no `!` without a comment
explaining why." Neither site carries one. The reason is real: the binding's nullability
annotation is wrong, and `identity` accepts null per Apple's documentation. That reason is what
the comment must state.

### F-9. `NearbyImplementation` carries three extractable machines — **confirmed**

The facade's partials total 939 lines and roughly twelve responsibilities. Nine of them are the
session's actual job: facade, pumps, flags, registry transitions, connection watch. Three are
self-contained policy machines with their own state:

- **Request expiry** — `_pendingRequests`, `_requestExpiries`, `ArmRequestExpiry`,
  `DisarmRequestExpiry`, `ExpireRequestAfterAsync` (`NearbyImplementation.cs:17-20`,
  `state.cs:132-197`).
- **Auto-accept policy** — the branch in `OnRequestReceived` plus `AutoAcceptAsync`
  (`state.cs:115-118`, `199-216`).
- **Discovery refresh loop** — `_refreshCts`, `_refreshTask`, `StartRefreshLoop`,
  `CancelRefreshLoop`, `DrainRefreshLoopAsync`, `RefreshDiscoveryLoopAsync`,
  `EvictAfterSettleAsync` (`NearbyImplementation.cs:32-33`, `state.cs:252-342`).

Each machine touches the rest of the session through a narrow surface: the registry, the gate,
one callback. Each has failure modes that deserve their own focused tests. A reader can
understand each one without the facade's other eleven concerns.

**Direction.** Extract the refresh loop and the expiry tracker as internal collaborator classes,
constructed by the session. Auto-accept can stay as a branch — it is eight lines — but track its
task (F-6). This is a quality refactor, not a defect fix. If R-5 also happens, do this first,
because it reduces the size of that later change.

### F-10. Small drifts — **confirmed**, all trivial

- **Misnamed callback class.** `AdvertiseCallback` (`Native/PlatformNearby.android.cs:761`) is a
  `ConnectionLifecycleCallback`, and `PlatformInitiateConnectAsync` constructs one on the
  *connect* path (`android.cs:438`). The name claims a scope the type does not have.
- **Double clear.** iOS `PlatformDispose` clears `PeerLookup` (`ios.cs:466`). The shared
  `DisposeAsync` then clears it again (`shared.cs:148`). Android's `PlatformDispose` does not
  clear. The sibling asymmetry shows that one side drifted.
- **Wrong exception in internal docs.** `Native/IPlatformNearby.cs:132-136` documents
  `InvalidOperationException` for a failed connect. The implementation faults the TCS with
  `NearbyException` everywhere.
- **Unlogged swallow.** The `Directory.Delete` catch in `NearbyConnection.SendAsync(FileResult)`
  (`Connections/NearbyConnection.cs:285-288`) swallows without a log line. The type has no
  logger, which is the underlying reason. For temp-file cleanup this is defensible. But the
  "every catch on an error path logs" convention says otherwise, and the repository adopted that
  convention because silent catches cost real debugging time before.

---

## 3. Re-litigated decisions

`AGENTS.md`, `DESIGN-PRINCIPLES.md`, or the code itself documents each decision below as
deliberate. The review judged each from scratch. Verdicts: **keep**, **keep-with-fix**,
**change**.

| # | Decision | Verdict |
|---|---|---|
| R-1 | Plain options object + eager validation, no `IOptions<T>` | keep-with-fix |
| R-2 | Factory-closure composition, internals not registered | keep |
| R-3 | Concrete `NearbyDeviceRegistry`, interface as read projection | keep |
| R-4 | `NearbyConnection` substitution via constructor delegates | keep |
| R-5 | One `sealed partial class` per-TFM platform layer | change |
| R-6 | Single-type `NearbyImplementation` facade | keep-with-fix |
| R-7 | Static `StagingDirectory` | change |
| R-8 | Drain-then-release as prose, not a type | keep-with-fix |
| R-9 | One `ILogger` instance, one category | keep |
| R-10 | TCS-per-handshake + shared `AwaitHandshakeAsync` | keep-with-fix |
| R-11 | MSTest v4 for the unit suite, hand-written fakes, no mocking library | change (framework) / keep (fakes) |

### R-1. Plain options, eager validation — keep-with-fix

**Original rationale** (`ServiceCollectionExtensions.cs:36-43`): the options pattern defers
validation to first resolution. MAUI apps never run `IHost.StartAsync`, so `ValidateOnStart`
never fires, and a bad `ServiceId` appears on whatever page first injects `INearby`.

**Against, from scratch.** `IOptions<T>` provides composition (`Configure` calls stack),
configuration binding, and a familiar shape. To give those up is a real cost. The documentation
states the cost honestly.

**Judgment.** The rationale survives scrutiny. On iOS a bad `ServiceId` is an unmanaged crash,
not an exception. A failure inside `AddNearby`, at the call site, is better than any deferred
pipeline. The needed fix is F-5: validation is only authoritative if the validated object cannot
change afterward. Snapshot it.

### R-2. Factory-closure composition — keep

**Original rationale** (implicit): `PlatformNearby`, `PeerLookup`, and the registry are
implementation details. The container registers what consumers resolve.

**Against, from scratch.** If the internals were registered, the container would manage their
lifetimes and show the graph in container tooling. The standing project direction says "prefer
Microsoft DI, minimize statics."

**Judgment.** Keep. Library guidance is to register the public service, not the private graph.
Internals in a host container are surface, and surface is what this project controls most
carefully. The DI direction is satisfied where it matters: everything is constructor-injected,
nothing is ambient, and `TryAddSingleton` respects host overrides for both `TimeProvider` and
`INearby`. The `CreatePlatformNearby` partial (`ServiceCollectionExtensions.cs:115`) extends the
file-suffix platform boundary into the composition root. That is checkable, and better than
`#if`. Record one caveat in the documentation: because the options live in the closure, a second
`AddNearby` call validates its own options and then silently discards them (`TryAddSingleton`
does nothing). A consumer that composes two registrations gets the first delegate's options,
with no signal.

### R-3. Concrete registry — keep

**Original rationale** (`AGENTS.md` → Tests): assert through the surface a consumer uses. Prefer
real objects over mocks where construction is cheap.

**Against, from scratch.** The write surface (`AddIfAbsent`, `Update`, `BeginGeneration`,
`EvictUnconfirmed`) is concrete-only, and the field is `new()`-ed inline
(`NearbyImplementation.cs:15`). The session cannot receive a substitute registry. The suite
already carries one cost from this: `TestSupport/FaultingDevices.cs` exists solely because
`FakeNearby` cannot fault the registry's stream from above.

**Judgment.** Keep, narrowly. The registry is deterministic and cheap to allocate. Its lock
discipline is part of what session tests should exercise, not fake away. `FaultingDevices` is
one small purpose-built double, which is an acceptable cost. If a second fault-injection need
appears, that is the trigger to inject the registry through the constructor — not before.

### R-4. Connection delegates instead of an interface — keep

The three delegates (`sendBytes`, `sendFile`, `dispose` —
`Connections/NearbyConnection.cs:113-125`) are exactly the platform-varying operations, and
`TestSupport/Create.Connection` substitutes them with one-liners. An `INearbyConnection`
interface would add public surface. It would invite consumers to mock behavior the library must
own: the receive-once guard and the disconnect contract. It would solve no problem the delegates
have. The sealed class with an internal constructor is the correct FDG shape for a handed-out
object.

### R-5. The platform layer as per-TFM partial classes — change

This is the review's largest recommendation. It re-litigates a documented position: the platform
event surface is "declared in prose, not as a type, on purpose" (`AGENTS.md` → device tests).

**The strongest case for the current design.** Partials are MAUI-idiomatic. There is zero
indirection: a callback writes straight into the shared bridge's channels. The prose contract
(SDK callbacks in → channel/TCS/registry effects out) is executable, because the device tests
drive it. An internal interface between bridge and SDK adapter is a second seam to maintain, and
seams are surface.

**The evidence against, accumulated in this codebase.**

1. The compiler cannot see the pairing. Six slips accumulated (F-4), one behavioral. The prose
   contract catches nothing at build time. It depends on a reviewer who remembers to grep the
   sibling — the exact dependence the repository's own "dominant defect class" warning
   describes.
2. The `net10.0` target is a stub of the *same class*, so the shared bridge is untestable where
   it matters. `Native/PlatformNearbyTests.cs:13-26` admits it outright: the tests "verify the
   write side, not the swap," because every enumeration path hits a
   `PlatformNotSupportedException` stub. The channel-swap logic is the most
   concurrency-sensitive shared code in the layer. It has no off-device test, and the gap is
   structural, not a missing test.
3. Each platform file carries fourteen responsibilities, because the partial gives them nowhere
   else to live. The files hold 675 and 638 code lines each, with the six longest methods in the
   codebase.

**The recommended shape.** Keep `PlatformNearby` as the one concrete `IPlatformNearby`
implementation. It holds everything now in `shared.cs` + `events.cs`: channels, TCS bookkeeping,
handshake deadlines, the work queue, staging, release ordering. Put the SDK translation behind a
small internal per-platform adapter. The adapter's operations are the current `Platform*` method
list — start/stop advertising and discovery, initiate/respond/abandon connect, send bytes and
file, release, availability. That list is already an interface in effect, expressed as partial
methods. The `net10.0` build gets a throwing adapter. Unit tests get a scripted adapter. This
closes the untestable-swap gap as a side effect, turns the prose contract into a
compiler-checked type, and gives the six duplicated shapes one home above the adapter.

**Cost, stated honestly.** Real churn in the two largest files, plus a new internal seam. The
device tests keep their value only if they keep the real adapters' callbacks as their entry
point. This is a pre-1.0-sized change. It is also the single change that retires the most
findings at once: F-4 completely, F-2 and F-3 partially, and the `PlatformNearbyTests`
admission. If the maintainer declines it, the fallback is to fold F-4's six slips into shared
helpers within the current partial shape, and to accept the untestable swap as permanent.

Weigh also the iOS backend migration off MultipeerConnectivity, which is planned for post-1.0.
That migration rewrites `PlatformNearby.ios.cs` wholesale. With an adapter seam, the migration
replaces one adapter and touches nothing shared. With the partial shape, the migration edits the
shared class's own file set again.

### R-6. Single-type facade — keep-with-fix

The facade itself is right: one public seam, one registration, no leaked orchestration. The fix
is F-9. The facade should coordinate the three policy machines, not contain them.

### R-7. Static `StagingDirectory` — change

See F-7. The documented rationale justifies `partial`, not `static`. The decision looks
deliberate but is unforced. That is why it gets a verdict here and not only a finding.

### R-8. Drain-then-release as prose — keep-with-fix

The reasoning for prose over a type is sound: the four drain sites differ in scope, and a shared
abstraction would have to be too general to say anything. But prose only works while it exists.
The fix is F-1. If R-5 is adopted, revisit this: with one bridge that owns all four sites, a
type may become the cheaper form.

### R-9. One logger, one category — keep

An outside reviewer first expects per-type categories, and `ILogger<INearby>` — a category named
after an interface — is unusual. But `docs/LOGGING.md` makes the single category an explicit
published contract: "one filter covers the whole library," with `EventId` ranges as the
discriminator. That is a defensible consumer-facing choice, not an accident. Consumers filter
one line. `EventId` ranges are already allocated per owning type, so structured sinks lose
nothing. A category change now breaks every consumer's filter configuration and provides no
functional gain. The window to adopt per-type categories closes at 1.0. Decide before the 1.0
tag. After that, never change it.

### R-10. TCS-per-handshake + shared deadline helper — keep-with-fix

`AwaitHandshakeAsync` works as designed: one helper, both platforms, deadline attribution pinned
by a device test that already caught a real bug. The fix is F-2. The cancel exit needs the same
cleanup the deadline exit has. Secondary note: the helper takes five parameters, and `Step<T>`
takes six, four of them delegates. Both sit at the edge of a legible parameter list. That alone
does not justify a restructure. R-5 would absorb both naturally.

### R-11. MSTest + hand-written fakes vs xUnit + NSubstitute — change the framework, keep the fakes

The maintainer raised this one directly, so the two halves get separate verdicts. They are
separate questions: the framework decides how tests run and read, and the mocking library
decides how seams are substituted.

**The facts.** The unit suite is MSTest v4: 46 files, 7,437 lines, 304 test methods, about 444
MSTest-specific assert calls. It uses zero `[TestInitialize]`/`[TestCleanup]` attributes — the
repository's own conventions already forbid them. The device suite is already xUnit v3, because
MSTest has no maintained on-device runner (`AGENTS.md` → device tests). The repository therefore
maintains two test frameworks and two convention sets today. `TestSupport/` holds ten
hand-written support files and no mocking library.

**Framework: change to xUnit v3.** Three reasons, in weight order:

1. **Consolidation.** One framework across the unit and device suites means one assert
   vocabulary, one analyzer set (`xunit.analyzers` is already pinned for the device suite), and
   one convention section in `AGENTS.md`. The current split exists only because MSTest cannot
   run on device — the constraint already picked xUnit once.
2. **Construction ergonomics.** xUnit builds a new class instance per test and injects shared
   state through constructors and `IClassFixture<T>`. That is the same construction-over-
   attributes style this suite already follows — the zero `[TestInitialize]` count shows the
   conversion is mechanical, not structural.
3. **Maintainer standard.** The maintainer's own rule set says xUnit for new code. A pre-1.0
   suite that contradicts its maintainer's standard is debt on every future test.

**The honest costs.** About 444 assert-call rewrites and 304 attribute swaps — mechanical, but
real review load. The `AGENTS.md` → Tests section needs a rewrite. Parallelism semantics change:
MSTest parallelizes per method, xUnit per class or collection, so suite wall-time can move in
either direction. The tests share no mutable state, so correctness does not move. One claim to
avoid: `dotnet test` support is not a differentiator. Both MSTest v4 and xUnit v3 run on
Microsoft.Testing.Platform, so either can restore `dotnet test` on the .NET 10 SDK with
configuration.

**Mocking library: keep the hand-written fakes.** The premise "DI-based unit tests are easier
with NSubstitute" holds for call-shaped interfaces: a method goes in, a value comes back. This
codebase's load-bearing seam is not call-shaped. `IPlatformNearby` returns
`IAsyncEnumerable<T>` streams, and `TestSupport/FakeNearby.cs:10-14` states why it is
deliberately not a mock: the completion and fault *timing* of those streams is the thing under
test. A substitute for that interface would still hand-build the channels — the library would
add a dependency without deleting code. `FaultingDevices` exists for the same reason, one layer
up. The review also found the suite's real testability pain (the untestable channel swap,
`ConstructionWitness`, the registry's fault gap) comes from design seams — the `net10.0` stub
and the inline-`new` registry — not from the absence of a mocking library. NSubstitute would
move none of it.

**The revisit trigger.** R-5's per-platform adapter *is* call-shaped: start, stop, connect,
send, release. If R-5 lands, NSubstitute fits that one seam well, and adopting it then — for the
adapter only — is reasonable. Adopting it now buys nothing the fakes do not already do better.

---

## 4. Non-findings

The review checked each item below deliberately and found it sound. The list shows the review's
coverage, not only its problems.

- **The DI seams.** An injected `TimeProvider` throughout — zero `DateTime.Now`/`DateTime.UtcNow`
  in src. No service locator, no stored `IServiceProvider`, no static `Instance`, no `Lazy<T>`.
  Host overrides respected via `TryAdd`. This is cleaner than the standing "DI audit planned"
  note implies. The audit's statics half is F-7 and little else.
- **All three interfaces pass the justification test.** Each has a load-bearing test double and
  a documented reason (`FakeNearby` for stream-fault timing, `FaultingDevices` for registry
  faults). The review found no speculative abstraction anywhere: no interface without a
  consumer, no factory for one product, no configuration for a constant.
- **`ChangeBroadcast`.** Subscribe-before-first-`MoveNext` and unsubscribe-in-`DisposeAsync` are
  both subtle and both correct, and the code documents the failure each one prevents
  (`ChangeBroadcast.cs:93-133`). The leak class that events had is retired.
- **`NearbyDeviceRegistry`.** Snapshot-array reads, lock-scoped writes, publish outside the
  lock, and reference-equality no-op detection with the reason stated
  (`NearbyDeviceRegistry.cs:142-149`).
- **`PeerLookup`.** Minted ids with the derivation history recorded. Sanitization at one choke
  point. `SafeDisplayName` closes the fifteen-site raw-name hole. First-name-wins is pinned by a
  test and annotated against a well-meant "fix" (`PeerLookup.cs:86-105`). This file holds the
  most careful security-relevant code in the repository.
- **`KeyedSerialQueue`.** Correct drain semantics, self-pruning tails, and queue tasks that
  never fault, with the error route named. The `Task.Run` inside it is load-bearing and
  commented.
- **`OutgoingTransfer`.** The `CancelAfter`-not-replace deadline reasoning
  (`OutgoingTransfer.cs:52-61`) records a real bug class and prevents it.
- **`async void` usage.** Confined to the two GMS override sites whose signatures the binding
  fixes. Both wrap the body in a catch-all routed to an error callback
  (`PlatformNearby.android.cs:767,805`). This is the sanctioned pattern, applied correctly.
- **Published identity.** Pinned explicitly in the csproj. The PublicAPI baselines are
  byte-identical across the three TFMs. All `.claude/rules/naming.md` §10 greps return empty.
- **Exception design.** Sealed `Nearby`-prefixed subclasses filed by domain, with
  message-required constructors. The classic FDG parameterless constructor is absent. That is
  acceptable modern practice, noted only so its absence reads as chosen.
- **Constructor escape sites.** The review examined both sites for a live defect and found none:
  `NearbyImplementation.cs:60` hands `this` to the lifecycle observer as the constructor's final
  statement, and `NearbyDeviceCollection.cs:118-128` starts its watcher after all fields are
  assigned in a sealed type. Both are fragile to ordering — a future field assigned below the
  call breaks silently. Fragility without a defect is a comment-level concern, not a finding.

---

## 5. Suggested sequencing

The order lets each step reduce the risk of the next. Steps 1–3 are small and independent.

1. **Restore the written contracts** (F-1). Recover or replace the two missing documents. Until
   the work list exists again, the findings in this document also have no durable home.
2. **Apply the mechanical fixes in one commit**: the `null!` comments (F-8) and the four small
   drifts (F-10). No behavior change, no test impact.
3. **Freeze the options at the boundary** (F-5). Small diff. It closes the validation bypass and
   makes the public doc true. Add the unit test that mutates after `AddNearby` and asserts no
   effect.
4. **Fix the cancel-path abandon** (F-2), with its device test. This is the one candidate
   correctness defect in the review.
5. **Give the connection dictionary one owner** (F-3). Do this after F-2, because F-2's fix
   determines which layer must own the dictionary.
6. **Extract the session's policy machines** (F-9), and make staging non-static (F-7).
7. **Decide R-5** — adapter seam versus status quo — as a deliberate, written decision. Every
   earlier step is compatible with either answer. If adopted, F-4 dissolves into it. If
   declined, execute F-4's fallback (shared helpers within the partials), so the six slips stop
   being six.
8. **Decide R-11's framework half** (MSTest → xUnit v3). If adopted, schedule the migration
   before step 3, because steps 3 through 6 add tests and every test added first is a test
   migrated later. The migration is mechanical and safe to run as its own commit. Defer the
   NSubstitute question until R-5 is decided — the adapter seam is the first place it would
   earn its dependency.

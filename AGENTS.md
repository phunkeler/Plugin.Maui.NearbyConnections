# AGENTS.md

Guidance for AI agents (GitHub Copilot, Claude Code, MCP-based assistants) and new contributors.

A .NET MAUI plugin for peer-to-peer connectivity with nearby devices, unifying Google's Nearby
Connections (Android) and Apple's MultipeerConnectivity (iOS). Shipped on NuGet as
`Plugin.Maui.NearbyConnections`.

## Commands

```bash
# All three TFMs must build warning-free — TreatWarningsAsErrors is on
dotnet build src/Plugin.Maui.NearbyConnections/Plugin.Maui.NearbyConnections.csproj -f net10.0
dotnet build src/Plugin.Maui.NearbyConnections/Plugin.Maui.NearbyConnections.csproj -f net10.0-android
dotnet build src/Plugin.Maui.NearbyConnections/Plugin.Maui.NearbyConnections.csproj -f net10.0-ios

# Unit tests — `dotnet run`, NOT `dotnet test` (VSTest is unsupported on the .NET 10 SDK)
dotnet run --project test/Plugin.Maui.NearbyConnections.UnitTests/Plugin.Maui.NearbyConnections.UnitTests.csproj

# Device tests — the platform partials on a real Android emulator / iOS simulator, no radio.
# Device setup (creating/booting an emulator or simulator) goes through two pinned local tools --
# AndroidSdk.Tool for Android, Microsoft.Maui.Cli for iOS and device enumeration (`dotnet tool
# restore` once per clone; the script does the rest). Boots a device if none is running; TRX
# results land in artifacts/. iOS leg needs macOS + Xcode.
# (This is the one place `dotnet test` DOES work: DeviceRunners.Testing.Targets replaces VSTest.)
./scripts/device-tests.ps1                          # -Platform defaults to all
./scripts/device-tests.ps1 -Platform android -AndroidApiLevel minimum   # or: latest | common | a literal API level

# .config/android-api-levels.json is the single source of truth for which Android API levels
# this suite tests (latest/common/minimum). CI runs all three as a matrix; a local run tests one
# at a time (default: latest). `minimum` tracks Directory.Build.props's Android
# SupportedOSPlatformVersion — bump both together, not independently. `common` is chosen by
# judgment against real-device distribution share, not derived from any repo constant.
# On-device coverage is not possible (see below) — this file adds test breadth, not coverage.

# Coverage — dotnet-coverage is pinned in .config/dotnet-tools.json, not globally installed.
# `dotnet tool restore` once per clone; the tool then runs as `dotnet dotnet-coverage`, not bare.
# test/coverage.runsettings scopes collection to the plugin DLL, excluding test/sample code.
dotnet tool restore
dotnet build -c Release test/Plugin.Maui.NearbyConnections.UnitTests/Plugin.Maui.NearbyConnections.UnitTests.csproj
dotnet dotnet-coverage collect "dotnet exec test/Plugin.Maui.NearbyConnections.UnitTests/bin/Release/net10.0/Plugin.Maui.NearbyConnections.UnitTests.dll" \
  --settings test/coverage.runsettings -f xml -o coverage.xml

# Browsable HTML report from the coverage.xml above — same tool ci.yml uses for the PR summary.
# coverage.xml and coveragereport/ are both gitignored; never commit either.
dotnet reportgenerator -reports:coverage.xml -targetdir:coveragereport -reporttypes:Html
open coveragereport/index.html   # macOS; use `start` on Windows, `xdg-open` on Linux
```

## The things people get wrong

- **`dotnet test` does not work for the unit suite.** Use `dotnet run --project`. (The device-test
  runner is the one exception — `DeviceRunners.Testing.Targets` replaces VSTest for it.)
- **The public API surface is build-enforced.** Anything newly `public` fails the build until
  recorded in `src/Plugin.Maui.NearbyConnections/PublicAPI/{tfm}/PublicAPI.Unshipped.txt`. Build,
  read the RS0016 errors, add the listed lines. Never suppress the analyzer to go green.
- **The dominant defect class is a fix applied to one platform partial and not its sibling.** When
  changing `*.android.cs`, grep `*.ios.cs` for the same shape, and vice versa.
- **`MCSessionState.Connecting` is not guaranteed to occur on iOS** — a peer can go straight to
  `NotConnected`. Treating it as a required waypoint is a latent hang.
- **A remote display name is untrusted input, and `PeerLookup.Record` is the only place it is
  cleaned.** Both platforms let the peer choose the string, and it reaches log sinks and consumer
  UI. `Record` strips control characters and caps the length; every device the library publishes is
  built there, on both platforms, so new code must route through it rather than passing a raw
  `EndpointName`/`MCPeerID.DisplayName` onward. The iOS `PeerKey` fallback sanitizes too — there the
  name becomes the device `Id`.
- **Every `catch` on a callback or error path must log.** Silent catches have already cost real
  debugging time here.
- **Published identity is locked before 1.0** — `PackageId`, `AssemblyName`, `RootNamespace`, and
  the repo name stay `Plugin.Maui.NearbyConnections`. The NuGet package carries the full published
  history; changing any of them orphans it. All three are pinned explicitly in the csproj because
  `AssemblyName`/`RootNamespace` otherwise derive from the project filename — so a project rename
  would change them silently. Internal type names, file names, and folders are **not** locked and
  may be reorganised freely; see `DESIGN-PRINCIPLES.md`.

## Architecture

One public interface, registered as a DI singleton (one radio, one native session):

- **`INearby`** — the only public entry point, implemented by `NearbyImplementation`
  (`NearbyImplementation.{cs,state.cs,log.cs}`, at the project root alongside the
  interface). Owns device state and the change stream. **It takes no dispatcher and has no UI
  thread affinity** — every member is callable from any thread.
- **`IPlatformNearby`** — internal. The raw platform streams, implemented by a single
  `sealed partial class` split across `Native/PlatformNearby.{shared,android,ios,net}.cs`.
  The `net10.0` target throws `PlatformNotSupportedException`, which is why `NearbyImplementation` depends
  on the interface rather than the concrete type — otherwise it is untestable off-device.

### State vs. streams — the split that matters

The division is not between discovery and connection phases but between *state* and *streams*:

| Shape | Used for | Why |
|---|---|---|
| State + deltas — `Devices` (snapshot) and `Devices.Changes` | Device presence and connection lifecycle | Presence *is* state, not a sequence of occurrences: the current set is readable at any time, so a consumer that starts late does not have to reconstruct it from history. `Changes` carries deltas rather than whole-list snapshots so nobody re-diffs on every transition. Every connection lifecycle transition arrives here — there are no separate connection events. |
| Stream — `NearbyConnection.ReceiveAsync` | Inbound payloads | Payloads are ordered, unbounded, and consumed once. The loop body is the seam where consumer async work goes, and it is awaited before the next payload is taken — a `void`-returning `EventHandler` cannot express that. See `docs/PAYLOAD-DELIVERY.md`. |

All async delivery is backed by `System.Threading.Channels`. Platform callbacks write into an
unbounded channel; the consumer's `await foreach` reads from it and suspends cheaply when empty.
That is why the API is a stream and not polling.

Two messaging primitives, chosen deliberately:

| Primitive | Used for | Why |
|---|---|---|
| `Channel<NearbyPayload>` (`SingleReader = true`) | Per-connection payload stream | Single-consumer data pipe: each payload is consumed exactly once. Two concurrent `ReceiveAsync` enumerators would race and steal items from each other, and unbounded channels accept writes unconditionally so no back-pressure would expose the bug. Single-consumer is enforced by construction; fan out above the plugin. |
| `TaskCompletionSource` | Disconnect signal (`NearbyConnection.Disconnected`) | One-time completion event: `Task` natively multicasts to any number of awaiters at zero cost. |

### Watch lifetime is the consumer's responsibility

`Devices.Changes` is a **broadcast** stream: every enumeration gets its own unbounded channel and
receives every change, independently of the others. (Contrast `NearbyConnection.ReceiveAsync`, which
is single-consumer because each payload must be handled exactly once.) It does **not** replay — read
`Devices` for the current state, then watch for what happens next.

Ending the enumeration is the only cleanup, and it cannot be forgotten: cancelling the token or
breaking out of the loop unregisters the watcher in a `finally`. This is what retired the leak class
that events had, where a missing `-=` kept the subscriber alive for the life of the app and fired
handlers N times after N page visits.

`samples/NearbyChat` shows the pattern: page ViewModels watch with `BasePageViewModel.NavigationToken`,
so navigating away ends the loop. Payload loops need no equivalent — they self-terminate when the
connection drops.

**Changes arrive on a thread-pool thread, not the platform's callback thread and never the UI
thread.** The SDK callback writes into a `PlatformNearby` channel; the pumps in
`NearbyImplementation.state.cs` drain it with `await foreach … ConfigureAwait(false)`, and every
registry write plus `Publish` happens on the reading side of that boundary. Every channel here is
built without `AllowSynchronousContinuations` — the registry's in `NearbyDeviceRegistry.Subscribe`,
the platform's in `PlatformNearby.NewChannel` — so a consumer's continuation is queued rather than
run inline on the publisher. That is fixed, not configurable: it was briefly a `NearbyOptions` knob,
and exposing it only offered consumers a way to stall the SDK's own callback dispatch with a slow
loop body. Do not reintroduce it. Do not document or rely
on the SDK's callback thread reaching consumers: it does not. Consumers that bind marshal for
themselves, or construct a `NearbyDeviceCollection<TRow>` — the one type in the library that knows a UI
thread exists.

The platform-callback threads themselves differ, and only one is documented. iOS `MCSessionDelegate`
calls arrive on "a private serial queue"
([Apple](https://developer.apple.com/documentation/multipeerconnectivity/mcsessiondelegate)); the
browser/advertiser delegates carry no equivalent Apple statement. Android GMS Nearby documents no
threading contract at all — Google's own samples touch UI directly inside
`onConnectionInitiated`, which implies main-thread delivery, but that is inference from sample code,
not a contract. Because of the pump, none of this is observable to consumers, which is why the
plugin's own invariant is thread-agnostic: the registry is thread-safe by construction and records
what a callback saw on whatever thread it arrived on.

### Lifecycle wiring is the app's responsibility

The plugin does not attach to the host app's lifecycle. Stopping advertising and discovery on
background is a product decision belonging to the app, not the library — and the platform (Android
Doze, iOS background limits) terminates the session anyway, with the callbacks flowing back through
the plugin as disconnection events.

**All device-state mutation goes through `NearbyImplementation.state.cs`**, which records it in
`NearbyDeviceRegistry`. The registry is thread-safe by construction — reads take an immutable
snapshot, writes are serialised by a lock — so platform callbacks record what they saw on whatever
thread they arrived on. Nothing in the library marshals to a UI thread except
`NearbyDeviceCollection<TRow>`.

### Two termination guarantees

Both platforms are callback-shaped. A callback API cannot hang: if the callback never arrives,
nothing was promised. This plugin converts those callbacks into awaitable operations and into
observable device state, and both conversions create a promise the platform does not make. These
two guarantees are what make that safe, and they are the reason the timeout options exist.

**1. Every public async operation terminates.** It returns, throws, or observes cancellation, within
a bounded time, on both platforms, whatever the radio does.

A pending handshake is a `TaskCompletionSource` in `_connectionTcs`. **Every failure path must
resolve or fault that TCS** or `AcceptAsync`/`ConnectAsync` hang forever. Resolving the TCS is the
mechanism, but it is not sufficient on its own — the platform may simply never call back. Google
documents `requestConnection` as completing when the request is *sent*, with no guarantee a callback
follows. So every await on a TCS is also bounded by a deadline the plugin owns:

| Operation | Bounded by |
|---|---|
| `ConnectAsync` | `ConnectTimeout` (30s), via `PlatformNearby.AwaitHandshakeAsync` |
| `AcceptAsync` | `AcceptTimeout` (15s), via the same helper — the window excludes the remote user's decision, so it is shorter by default |
| `SendAsync` (file) | `TransferInactivityTimeout`, via `OutgoingTransfer.InactivityToken` |
| `StartAdvertisingAsync` / `StartDiscoveryAsync` | `started` resolves on both branches; iOS adds `Apple.StartFailureGraceWindow` |

`AwaitHandshakeAsync` is shared, not per-platform, on purpose: the accept path once awaited the
caller's token alone on both platforms, so an accepted handshake whose peer left range never
returned at all. One helper is what stops that from recurring in one partial and not its sibling.
**A new await on a platform callback belongs in that helper, or needs its own documented deadline.**

`Timeout.InfiniteTimeSpan` opts out of this guarantee, deliberately. An operation configured that
way waits forever by the consumer's own choice.

**A cancelled handshake has three possible sources, and only one of them is a timeout.** The caller's
token, the deadline, and `DisposeAsync` settling the pending TCS all surface as
`OperationCanceledException`. `AwaitHandshakeAsync` therefore tests `deadlineCts` directly rather
than inferring "not the caller's token", which would report a teardown as an elapsed deadline that
never elapsed. `DisposeTests.DisposeMidHandshake_CancelsPendingAccept` is the device test that
catches this, and it caught it once already.

`NearbyConnection.Disconnected` and `ReceiveAsync` are outside the guarantee and do not need a
deadline: neither promises completion, so neither can hang. A connection that never drops simply
never completes `Disconnected`.

**2. Every device state is transient or terminal.** No device sits indefinitely in a state it cannot
leave. This is the state-shaped counterpart, and it is not the same as guarantee 1 — no caller is
awaiting anything, so nothing hangs. What breaks instead is the device set: a row stuck in a state
whose underlying platform handle is already dead. `RequestReceived` is bounded by
`InboundRequestTimeout` for exactly this reason.

**Neither guarantee covers work the session starts on its own behalf** — auto-accept, request
expiry, disconnect watchers, inbound file copies. Nothing awaits those, so disposal cannot tell
whether they finished. `docs/CONCURRENCY.md` diagrams that third category, records which sites are
drained today, and is the place to read before adding another `_ = SomeAsync(...)`.

### Drain, then release

The two guarantees above are promises to a caller. This one is internal, and it is what makes
disposal safe rather than merely prompt.

Every teardown path waits for the work that reads a handle before freeing the handle. Cancellation
is not a join: `CompleteReceive` and `Cancel()` set a flag and return, so an inbound copy can still
be mid-write when the next line disposes what it is writing to. The order is the guarantee — not a
tidiness preference — and it applies at every scope:
`ReleaseConnectionAsync` for one endpoint, `PlatformNearby.DisposeAsync` for the session,
`AppLifecycleObserver.DisposeAsync` for the iOS backgrounding observer.

Two consequences bind new code:

- **A release that frees handles must be awaitable.** A `void` one cannot obey the rule. Where the
  call site is a platform callback whose signature the binding fixes, route it through
  `ReleaseConnectionFromCallback` — never `async void`, where an exception reaches the SDK's thread
  and terminates the process.
- **Every drain is bounded.** A wedged native read must not turn disposal into a hang. The bounds
  are constants, not `NearbyOptions` knobs: they exist so disposal terminates, and no consumer
  scenario wants a different value.

`docs/CONCURRENCY.md` lists the four drain sites and explains why the rule is prose rather than a
shared type.

Platform code lives in platform partials, never `#if` in shared logic. When shared code needs a
platform-specific step, the sanctioned mechanism is the **platform hook pair**: shared code declares
a `partial void` (e.g. `PlatformInitializeLifecycleObserver` in `NearbyImplementation.cs`), exactly
one platform file implements it, and on every other platform the call compiles to nothing — no
`#if`, no stub file, no unused parameter.

### Folder layout

The four domain folders are the platform-neutral model; `Native/` is the layer that maps Google's
Nearby Connections and Apple's MultipeerConnectivity onto it. The tree is meant to state that
claim — if translation logic starts appearing outside `Native/`, the abstraction is leaking.

```
src/Plugin.Maui.NearbyConnections/
├── INearby.cs                     facade — at the root because it spans every domain
├── NearbyImplementation.{cs,state.cs,log.cs}
├── NearbyException.cs             root exception — at the root for the same reason
├── MauiAppBuilderExtensions.cs               registration entry points, beside the facade
├── ServiceCollectionExtensions.cs
├── Connections/   NearbyConnection, request, role, ControlMessage, connect timeout
├── Devices/       NearbyDevice (immutable record), INearbyDevices + NearbyDeviceRegistry,
│                  NearbyDeviceChange(+Action), NearbyDeviceCollection<TRow>, status,
│                  EndReason (internal — log-only)
├── Discovery/     availability + advertising/discovery failures
├── Payload/       NearbyPayload + NearbyBytesPayload/NearbyFilePayload — the data
├── Transfer/      progress, transfer timeout, outgoing transfer — the act of moving it
├── Options/       NearbyOptions + platform scopes + validator (iOS-only rules) + the enums
├── Native/        IPlatformNearby, PlatformNearby.*, PeerLookup{,.ios} — this layer's own
│                  peer bookkeeping, NOT the session's device set (the .ios half adds the
│                  MCPeerID handle plus peer-key derivation),
│                  AppLifecycleObserver.ios
└── Platforms/     MAUI SDK convention folder (Android permissions) — NOT the same as Native/
```

**Nothing in `Native/` is public.** That is the quarantine, and it is checkable: a `public` type
declared there means the translation layer has leaked into the API surface.

The unit test project mirrors this layout, so a type's tests live in the folder matching its own.

**`Native/` vs `Platforms/`:** `Platforms/` is reserved by the MAUI SDK and carries its own
include/exclude rules. `Native/` is this plugin's translation layer. They are different things that
would read as the same thing if `Native/` were called `Platform/` — which is why it is not.

## Conventions

Mechanical style (braces, accessibility modifiers, `_camelCase` fields, `var` usage) is enforced by
`.editorconfig` + `TreatWarningsAsErrors` — the build fails on drift, so it is not restated here.
File-scoped namespaces are convention, not enforced.

- Nullable enabled — no `!` without a comment explaining why.
- `CancellationToken` on every public async method that does I/O.
- Logging via source-generated `ILogger` partial methods in `*.log.cs`, never string interpolation.
  Give every `[LoggerMessage]` an explicit `EventId`/`EventName` — ranges are reserved per owning
  type (documented at the top of `NearbyImplementation.log.cs`) so an id is stable across edits and
  never reused once shipped. Pass the `Exception` object on an `Error`-level method, never
  `ex.Message` — the trailing-`Exception`-parameter form is what lets a structured sink capture the
  stack trace and type. Use the instance form (`partial void LogXxx(...)` against a captured
  `_logger`) when the type constructs with an injected logger; use the static form
  (`static partial void LogXxx(ILogger logger, ...)`) when the logger instead arrives via an
  injected property/parameter on a type that does not own it (e.g. `PeerLookup`) — both are
  correct, the choice follows who owns the logger.
- Device display names and file paths appear in log messages at `Error`/`Debug` levels by default —
  this is standard `ILogger` behaviour, not a defect; configure a minimum log level per category in
  the host app if that identity data should not reach a sink.
- Errors surface as typed exceptions (`NearbyException` and subclasses) at the public
  boundary. Never return `null` to signal failure.
- `ChannelWriter.TryComplete` returns `bool` — a `false` return means the fault was dropped and the
  consumer sees a normal end-of-stream. Log it.
- Scope broad catches with a filter, not a rethrowing catch:
  ```csharp
  catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
  ```
- Public members need XML docs (enforced). **Document platform divergence on the member itself.**
- Public types stay vendor-neutral: `Peer` is Apple's vocabulary, `Endpoint` is Google's. Internal
  code may use the platform's own term. The binding contract is `.claude/rules/naming.md` (loads
  automatically for `src/**`); `DESIGN-PRINCIPLES.md` explains the reasoning and holds the questions
  that are still open — do not resolve those silently.

## Tests

MSTest v4 with strict Arrange / Act / Assert — blank line between sections, all three comments
present even when trivial. Compute expected values in Arrange; no logic in Assert.

MSTest specifics: `Assert.ThrowsExactly<T>` (not `ThrowsException<T>`), `Assert.IsEmpty(x)`,
`Assert.HasCount(n, x)` (count first). Class names cannot end in `Collection`. Parallelization is
method-level, so tests must not share mutable state.

**Assert through the surface a consumer uses.** Public API first; internals widened by
`InternalsVisibleTo` are fair game where the type is itself internal (`PlatformNearby`,
`PeerLookup`). Private field names are not — a test coupled to one passes when the behaviour
breaks and fails when a safe rename happens. The two deliberate exceptions each carry a comment
saying why, and both exist because `net10.0` cannot reach the behaviour any other way; do not add a
third without the same justification. Equally, do not test the compiler (generated record members)
or the BCL (`CancellationTokenSource` firing on a `FakeTimeProvider`) — that is someone else's
implementation.

**Supporting code lives in `TestSupport/`, never in a test file.** A `*Tests.cs` file contains its
test classes and test methods and nothing else — no factories, no fakes, no constants — so it reads
top to bottom as tests. `TestSupport/Create.cs` builds the types under test; `FakeNearby` is the
suite's one test double, standing in for the `IPlatformNearby` seam. Helpers carry XML docs (they
are read apart from their call sites); tests do not (the name is the doc).

A change that touches the same construction shape at 3+ test call sites (e.g. a constructor
signature edit rippling through several `new NearbyConnection(...)` sites) is not done when it
compiles and tests pass — check whether a `TestSupport/Create.*` factory already covers that shape
and route every call site through it before reporting the change complete. This is the mechanical
form of the rule above; it is easy to satisfy the letter of "use `Create.*`" while still hand-rolling
construction at a handful of sites a mechanical find-and-replace didn't think to touch. For general
test-quality convention beyond this repo's own rules, weigh changes against Microsoft's
[Unit testing best practices for .NET](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)
rather than relying on memory of a past review — an agent with no prior context on this repo can
fetch that page directly and re-derive the same checks (AAA structure, one Act per test, no magic
values, minimal input, helper methods over `[TestInitialize]`/`[TestCleanup]`).

**When adding or changing tests, generate coverage** (Commands, above) and check the delta on the
files the change actually touches — a new test that doesn't move coverage on its target method
usually means it isn't exercising the path it claims to. There is currently no enforced coverage
threshold; `Native/PlatformNearby.*` sits far below whatever a repo-wide number would require
because platform partials need real SDKs (see below) and cannot be raised from `net10.0`, so a
blanket percentage gate would either block on that permanently or have to be file-scoped. Coverage
is visible in CI (`ci.yml` posts a per-PR summary via SonarQube) — read that before adding a second,
possibly conflicting coverage gate.

**UI tests** (`test/NearbyChat.UiTests/`, Appium) locate elements by resource-id (`MobileBy.Id`),
not `AccessibilityId` — MAUI clears `content-desc`. Never change an `x:Name` or `AutomationId`
without updating that suite. It is historically flaky for environment reasons; triage a red run
against known flakes before blaming a code change.

**Device tests** (`test/Plugin.Maui.NearbyConnections.DeviceTests{,.Runner}/`) are the automated
check on the platform partials — xUnit v3 hosted in a MAUI runner app via DeviceRunners, driven by
`scripts/device-tests.ps1` on an Android emulator / iOS simulator (locally and in `device-tests.yml`).
No radio, no multi-device: tests invoke the internal callbacks (`OnConnectionResult`,
`OnPeerStateChanged`, …) with directly-constructed SDK argument objects — the same pattern
dotnet/maui's Essentials device tests use — and assert through the channels/TCS/registry the
callbacks feed. Multi-device flows remain the UI suite's job. Conventions that differ from the
unit suite, deliberately:

- **xUnit (`[Fact]`), not MSTest.** MSTest has no maintained on-device runner (MSTestX predates
  modern `net*-android` and fails `XA1039`); DeviceRunners supports xUnit v3 with `dotnet test`
  TRX collection. AAA structure and TestSupport rules still apply.
- **Some GMS argument types only construct through deprecated-but-shipped ctors**
  (`ConnectionResolution`, `ConnectionInfo`, `DiscoveredEndpointInfo`). Each such call site wraps
  exactly one line in `#pragma warning disable CS0618` — if a binding bump removes the ctor, the
  suite fails loudly at compile time, which is the failure mode we want. Never widen the pragma.
- **No on-device code coverage.** `dotnet-coverage`/coverlet cannot instrument the Android/iOS app
  runtimes; the deliverable is TRX results (in `artifacts/`, surfaced as CI checks), not a
  coverage delta. Do not add a coverage step to the device jobs — it will not work.
- **The device suite runs serially** — `AssemblyMarker.cs` carries
  `[assembly: CollectionBehavior(DisableTestParallelization = true)]`. `PlatformNearby.StagingDirectory`
  is static and process-wide, and every `DisposeAsync` sweeps it, so a test disposing its platform
  deletes whatever another test staged. Nearly every test disposes one via `await using var platform`.
  Do not re-enable parallelism to speed the suite up: it runs in a couple of seconds, and the failure
  it buys back is an intermittent `FileNotFoundException` in a *different* test than the one at fault,
  reproducing only on slower API levels.
- **Pass the device explicitly.** DeviceRunners' booted-simulator auto-detection is unreliable;
  `scripts/device-tests.ps1` always passes `-p:DeviceRunnersDevice=<id>`. Do the same in any manual
  `dotnet test` invocation.
- **Device setup goes through two pinned local tools**, both restored via `dotnet tool restore` —
  one setup algorithm shared by `device-tests.ps1` and `device-tests.yml`, replacing hand-rolled
  adb/simctl calls:
  - **`AndroidSdk.Tool`** (`dotnet android ...`) owns Android emulator lifecycle: `sdk
    accept-licenses --force`, `sdk install --package ...`, `avd create --name ... --sdk ...`,
    `avd start --name ... --wait-boot --cpu-threshold N --response-threshold N`. Its `avd start`
    has native headless flags (`--no-window`, `--gpu`) and boot-readiness checks the maui CLI
    lacks — `--cpu-threshold`/`--response-threshold` wait for the guest to settle after
    `sys.boot_completed`, not just report it, which is a real gap `getprop` polling has: a device
    can report boot-completed while still under first-boot CPU load. Its JSON output
    (`--format json`) is PascalCase (`Name`, `Target`, `Device`) — a different casing convention
    than `dotnet maui`'s, so do not assume one schema applies to both tools.
  - **`Microsoft.Maui.Cli`** (`dotnet maui ...`) owns iOS simulator lifecycle (`apple simulator
    create/start/list`) and cross-platform device enumeration (`device list --json`) — Android
    still reads through it for `is_running` checks alongside `dotnet android`. `dotnet maui device
    list --json` returns snake_case fields (`identifier`, `is_running`, `is_emulator`,
    `details.avd`); `dotnet maui apple simulator create --json` returns a different shape (`udid`,
    not `identifier`) — do not assume one schema applies to both commands.
  - `AndroidSdk.Tool` has no iOS/Apple equivalent, so this stays a two-tool split by design, not a
    full migration off `dotnet maui`.
  - **AVD create and boot happen only inside `Invoke-AndroidTests`** — `device-tests.yml`'s
    Android job has no separate create/boot steps; its "Run device tests on emulator" step calls
    `device-tests.ps1` directly, and the function's own `avd list` check (skip create when the AVD
    already exists) does the work the workflow's cache-hit conditional used to do explicitly. One
    place decides whether to create, not two kept in sync by hand.
  - **Arch and GPU mode are host-detected, not hard-coded.** `-AndroidArch` defaults to the host's
    `uname -m` (`arm64-v8a` on Apple Silicon, `x86_64` elsewhere) so a Mac runs a native image
    instead of one under emulation; `-AndroidGpu` defaults to `swiftshader_indirect` (software
    rendering, required on CI's KVM host with no real GPU) when `$env:CI` is set, `auto` (real
    hardware acceleration) otherwise. Override either to reproduce a specific CI leg locally. The
    AVD name embeds arch (`device-tests-{level}-{arch}`) so switching `-AndroidArch` on one machine
    can't collide with an AVD already created for the other arch.
- **CI tests Android across a 3-level matrix** (`.config/android-api-levels.json`:
  latest/common/minimum), one leg per level, each with its own AVD, cache key, TRX file, and
  uploaded artifact. A local run tests one level at a time (`-AndroidApiLevel`, default `latest`).

The device tests deliberately do **not** test through `IPlatformNearby` — they drive the internal
callbacks and assert on the internal channels/TCS map/registry. That surface (SDK callbacks in →
channel/TCS/registry effects out) is a deliberate second contract, *the platform event surface*:
it is what any new platform backend must satisfy, and the device tests are its executable
specification. It is declared in prose, not as a type, on purpose.

## Further reading

- `docs/DEVICE-LIFECYCLE.md` — device lifecycle states and platform capability gaps
- `docs/PAYLOAD-DELIVERY.md` — why payloads are delivered as events, not an async stream
- `docs/CONCURRENCY.md` — which async work the session owns, who awaits it, and what disposal may
  assume. Diagrams the task layers behind inbound payloads, and lists the sites where nothing
  awaits the work yet.
- `docs/LOGGING.md` — the consumer-facing level contract, categories, and EventIds. **Adding or
  re-levelling a log message means updating that table** — it is the published contract, not a
  summary of the code.
- `CONTRIBUTING.md` — build, release, and contribution process

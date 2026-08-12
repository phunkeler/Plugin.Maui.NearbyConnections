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

- **`dotnet test` does not work here.** Use `dotnet run --project`.
- **The public API surface is build-enforced.** Anything newly `public` fails the build until
  recorded in `src/Plugin.Maui.NearbyConnections/PublicAPI/{tfm}/PublicAPI.Unshipped.txt`. Build,
  read the RS0016 errors, add the listed lines. Never suppress the analyzer to go green.
- **The dominant defect class is a fix applied to one platform partial and not its sibling.** When
  changing `*.android.cs`, grep `*.ios.cs` for the same shape, and vice versa.
- **`MCSessionState.Connecting` is not guaranteed to occur on iOS** — a peer can go straight to
  `NotConnected`. Treating it as a required waypoint is a latent hang.
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

**Changes arrive on the platform's callback thread.** Consumers that bind marshal for themselves, or
construct a `NearbyDeviceCollection` — the one type in the library that knows a UI thread exists.

### Lifecycle wiring is the app's responsibility

The plugin does not attach to the host app's lifecycle. Stopping advertising and discovery on
background is a product decision belonging to the app, not the library — and the platform (Android
Doze, iOS background limits) terminates the session anyway, with the callbacks flowing back through
the plugin as disconnection events.

**All device-state mutation goes through `NearbyImplementation.state.cs`**, which records it in
`NearbyDeviceRegistry`. The registry is thread-safe by construction — reads take an immutable
snapshot, writes are serialised by a lock — so platform callbacks record what they saw on whatever
thread they arrived on. Nothing in the library marshals to a UI thread except
`NearbyDeviceCollection`.

A pending handshake is a `TaskCompletionSource` in `_connectionTcs`. **Every failure path must
resolve or fault that TCS** or `AcceptAsync`/`ConnectAsync` hang forever.

Platform code lives in platform partials, never `#if` in shared logic.

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
│                  NearbyDeviceChange(+Action), NearbyDeviceCollection, status,
│                  EndReason (internal — log-only)
├── Discovery/     availability + advertising/discovery failures
├── Payload/       NearbyPayload + NearbyBytesPayload/NearbyFilePayload — the data
├── Transfer/      progress, transfer timeout, outgoing transfer — the act of moving it
├── Options/       NearbyOptions + platform scopes + validator (iOS-only rules) + the enums
├── Native/        IPlatformNearby, PlatformNearby.*, PeerRegistry{,.ios} — this layer's own
│                  peer bookkeeping, NOT the session's device set (the .ios half adds the
│                  MCPeerID handle), iOS peer identity, AppLifecycleObserver.ios
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
  injected property/parameter on a type that does not own it (e.g. `PeerRegistry`) — both are
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
`PeerRegistry`). Private field names are not — a test coupled to one passes when the behaviour
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

Platform partials have **no automated unit coverage** — they need real SDKs. The UI suite is the
only automated check on that code.

## Further reading

- `docs/DEVICE-LIFECYCLE.md` — device lifecycle states and platform capability gaps
- `docs/PAYLOAD-DELIVERY.md` — why payloads are delivered as events, not an async stream
- `CONTRIBUTING.md` — build, release, and contribution process

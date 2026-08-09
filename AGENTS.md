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
  interface). Owns device state, dispatcher marshalling, and the three lifecycle events.
- **`IPlatformNearby`** — internal. The raw platform streams, implemented by a single
  `sealed partial class` split across `Native/PlatformNearby.{shared,android,ios,net}.cs`.
  The `net10.0` target throws `PlatformNotSupportedException`, which is why `NearbyImplementation` depends
  on the interface rather than the concrete type — otherwise it is untestable off-device.

### State vs. streams — the split that matters

The division is not between discovery and connection phases but between *state* and *streams*:

| Shape | Used for | Why |
|---|---|---|
| Observable state — `Devices` + three C# events | Device presence and connection lifecycle | Presence *is* state, not a sequence of occurrences. A collection can be bound directly and read at any time; an event stream forces every consumer to rebuild the same collection from deltas. `NearbyDevice.Status` changes in place, so a bound row updates rather than moving between collections. |
| Stream — `NearbyConnection.ReceiveAsync` | Inbound payloads | Payloads are ordered, unbounded, and consumed once. The loop body is the seam where consumer async work goes, and it is awaited before the next payload is taken — a `void`-returning `EventHandler` cannot express that. See `docs/PAYLOAD-DELIVERY.md`. |

All async delivery is backed by `System.Threading.Channels`. Platform callbacks write into an
unbounded channel; the consumer's `await foreach` reads from it and suspends cheaply when empty.
That is why the API is a stream and not polling.

Two messaging primitives, chosen deliberately:

| Primitive | Used for | Why |
|---|---|---|
| `Channel<NearbyPayload>` (`SingleReader = true`) | Per-connection payload stream | Single-consumer data pipe: each payload is consumed exactly once. Two concurrent `ReceiveAsync` enumerators would race and steal items from each other, and unbounded channels accept writes unconditionally so no back-pressure would expose the bug. Single-consumer is enforced by construction; fan out above the plugin. |
| `TaskCompletionSource` | Disconnect signal (`NearbyConnection.Disconnected`) | One-time completion event: `Task` natively multicasts to any number of awaiters at zero cost. |

### Subscription lifetime is the consumer's responsibility

The session is a singleton, so an event subscription without a matching `-=` keeps the subscriber
alive for the life of the app, and re-subscribing (re-navigating to a page) fires handlers N times
after N visits. Event handlers must also be fast and must not do I/O — they run synchronously on the
dispatcher. A throwing handler is caught and logged so it cannot take down the callback path, but it
still starves the handlers after it.

`samples/NearbyChat` shows the required pattern: `BasePageViewModel.RegisterSessionSubscription`
detaches on navigate-away, and no page ViewModel subscribes directly. Payload loops need no
equivalent — they self-terminate when the connection drops.

### Lifecycle wiring is the app's responsibility

The plugin does not attach to the host app's lifecycle. Stopping advertising and discovery on
background is a product decision belonging to the app, not the library — and the platform (Android
Doze, iOS background limits) terminates the session anyway, with the callbacks flowing back through
the plugin as disconnection events.

**All device-state mutation goes through `NearbyImplementation.state.cs` and is dispatcher-marshalled.**
Platform callbacks arrive on background threads; mutating an `ObservableCollection` or raising
`PropertyChanged` off the UI thread crashes XAML binding.

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
├── Connections/   NearbyConnection, request, role, ControlMessage, EventArgs, connect timeout
├── Devices/       NearbyDevice, DeviceState, status, events, EndReason
├── Discovery/     availability + advertising/discovery failures
├── Payload/       NearbyPayload + NearbyBytesPayload/NearbyFilePayload — the data
├── Transfer/      progress, transfer timeout, outgoing transfer — the act of moving it
├── Options/       NearbyOptions + platform scopes + validators + the enums they use
├── Native/        IPlatformNearby, PlatformNearby.*, iOS peer identity,
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

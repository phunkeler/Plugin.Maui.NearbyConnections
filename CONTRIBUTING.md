# Contributing

## Architecture

### Two phases, two tiers

Every session goes through two phases: **discovery/advertising** (learning who is nearby) and **connection** (exchanging data with a specific peer). These phases map directly to the two stream types the plugin exposes.

The codebase has two layers:

| Tier | Type | Responsibility |
|------|------|----------------|
| 1 | `INearbyConnections` | Raw platform streams. `AdvertiseAsync` yields inbound connection requests; `DiscoverAsync` yields device visibility events; `ConnectAsync` establishes a connection. No state ownership, no threading concern. |
| 2 | `INearbyAdvertiser` / `INearbyDiscoverer` | MAUI-friendly services. Absorb loop hosting, lifecycle state, and unified event delivery. `EventsAsync` merges connection lifecycle and payload events into one stream with atomic current-state replay on subscribe. |

### Stream primitive — `System.Threading.Channels`

All async delivery is backed by `System.Threading.Channels`. Platform callbacks (Bluetooth/WiFi hardware events, arriving bytes) write into an unbounded channel; consumer `await foreach` reads from it. When there is nothing to read the read suspends cheaply until a write occurs. This is why the API is a stream and not polling.

`NearbyConnection` wraps its own per-connection `Channel<NearbyPayload>`. The `INearbyAdvertiser` / `INearbyDiscoverer` services forward each active connection's payloads as `PayloadReceived` events through the same per-subscriber `ChannelBroadcaster` fan-out that carries their lifecycle events (`ConnectionLifecycle.cs`, `ForwardPayloadsAsync`).

### Three messaging patterns

The library uses three distinct primitives, each matched to the semantics of its use case:

| Pattern | Used for | Why |
|---|---|---|
| `ChannelBroadcaster<T>` | Advertiser / discoverer events (`EventsAsync`) | Fan-out: each subscriber gets its own copy of every event. Multiple observers (e.g. a ViewModel and a background service) can all watch the same lifecycle stream independently. |
| `Channel<NearbyPayload>` (single, `SingleReader = true`) | Per-connection payload stream (`NearbyConnection.ReceiveAsync`) | Single-consumer data pipe: each payload is consumed exactly once. Two concurrent `ReceiveAsync` enumerators on the same connection would race and steal items from each other — unbounded channels accept writes unconditionally so there is no back-pressure to expose the bug. The design enforces single-consumer by construction. |
| `TaskCompletionSource` | Disconnect signal (`NearbyConnection.Disconnected`) | One-time completion event: `Task` natively multicasts to any number of awaiters at zero cost. No channel or broadcaster needed. |

### EventsAsync snapshot replay

`EventsAsync` on the tier-2 services yields current state as synthetic events — under a lock — before handing off to the live channel. This eliminates the read-snapshot / subscribe-INCC race that affects any design built on separate snapshot + event-notification primitives. A `Synchronized` sentinel event marks the boundary between replayed history and live events.

### Channel lifetime

The tier-2 services use a **fan-out per-subscriber channel** model. Each call to `EventsAsync()` creates a private `Channel<T>` that is registered atomically (under the same lock that captures the current-state snapshot), so the subscriber receives a consistent snapshot followed by live events with no race window.

Subscriber channels are completed only when:
- The caller's `CancellationToken` fires, or
- `Dispose()` / `DisposeAsync()` is called on the service

`StartAsync()` and `StopAsync()` do **not** complete channels — they control the internal run loop and emit cleanup events (`ConnectionRequestExpired`, `DeviceLost`) so subscribers update their UI. A consumer that subscribes with `EventsAsync(navigationToken)` survives multiple `StartAsync`/`StopAsync` cycles on the same service instance.

Do not rely on `StopAsync()` to terminate a stream — use the caller's cancellation token or `Dispose()` instead.

### DI registration

The plugin follows a builder pattern with two entry points:

**MAUI apps** — use `UseNearbyConnections()` on `MauiAppBuilder` (the MAUI-idiomatic style):

```csharp
builder.UseNearbyConnections(opts =>
    {
#if IOS
        opts.InvitationTimeout = TimeSpan.FromSeconds(10);
#endif
    })
    .AddAdvertiser()   // opt-in: INearbyAdvertiser (Tier 2)
    .AddDiscoverer();  // opt-in: INearbyDiscoverer (Tier 2)
```

**Testing seam** — `AddNearbyConnections()` also exists directly on `IServiceCollection`, and `INearbyConnections` is public, because consumers mock or implement the interface to test their own app code against the plugin. The public test-double constructors on `NearbyConnection` (`Connections/NearbyConnection.cs`) and `NearbyConnectionRequest` (`Connections/NearbyConnectionRequest.cs`) exist for exactly this: they let a fake `INearbyConnections` yield real connection/request objects into the code under test.

```csharp
services.AddNearbyConnections()
    .AddAdvertiser()
    .AddDiscoverer();
```

`AddAdvertiser()` / `AddDiscoverer()` are explicit opt-in calls because they register the optional `INearbyAdvertiser` / `INearbyDiscoverer` services as singletons. Apps that only need the core `INearbyConnections` API can omit them.

### Lifecycle wiring — app responsibility

The plugin does not attach to the host app's lifecycle. Stopping advertising and discovering when the app backgrounds is a product decision that belongs to the app, not the library. The platform (Android Doze, iOS background limits) terminates Nearby Connections / Multipeer sessions anyway, and the platform callbacks flow back through the plugin as disconnection events.

Apps that want to stop proactively — for example, to release Bluetooth/WiFi scan locks before the OS does — can wire lifecycle events themselves:

```csharp
// MauiProgram.cs
builder.ConfigureLifecycleEvents(lifecycle =>
{
#if ANDROID
    lifecycle.AddAndroid(android => android.OnStop(activity =>
    {
        var sp = IPlatformApplication.Current?.Services;
        _ = sp?.GetService<INearbyAdvertiser>()?.StopAsync();
        _ = sp?.GetService<INearbyDiscoverer>()?.StopAsync();
    }));
#elif IOS
    lifecycle.AddiOS(ios => ios.DidEnterBackground(app =>
    {
        var sp = IPlatformApplication.Current?.Services;
        _ = sp?.GetService<INearbyAdvertiser>()?.StopAsync();
        _ = sp?.GetService<INearbyDiscoverer>()?.StopAsync();
    }));
#endif
});
```

Use `OnStop` on Android and `DidEnterBackground` on iOS — these fire only on true backgrounding, not for transient interruptions such as notifications, dialogs, or incoming calls. The DI singletons remain alive across background/foreground cycles; pages that call `StartAsync()` on `NavigatedTo` will resume naturally when the user returns.

### Platform implementations

Each platform implements `INearbyConnections` as a partial class sealed against `NearbyConnectionsImplementation`. Platform-specific files are excluded from non-matching build targets via `src/Directory.Build.targets`. Global usings per platform are also injected there.

## Day-to-day development

1. Work on a feature branch, not directly on `main`:
   ```bash
   git checkout -b feat/my-feature
   ```

2. Commit using the [Conventional Commits](#commit-messages) format — this is what drives the changelog and version bump.

3. Open a PR targeting `main`. The SonarCloud build must pass before merging.

4. Squash or merge into `main`. release-please will pick up the changes automatically.

## Releasing

Releases are fully automated once changes land on `main`. You do not manually edit versions anywhere.

### Normal release (patch or minor)

1. Commits accumulate on `main` via merged PRs.
2. [release-please](https://github.com/googleapis/release-please) automatically maintains an open **Release PR** titled `chore(main): release x.y.z`. It updates `version.txt` and maintains `CHANGELOG.md` (the file is created when the first Release PR merges) as new commits land.
3. When you're ready to ship, **merge the Release PR**.
4. Merging creates a git tag (e.g. `v0.2.0`) and a GitHub Release.
5. The tag triggers the `publish` workflow → approve the `nuget` environment deployment in GitHub Actions → package is pushed to NuGet.org.

### Pre-release (preview, rc)

release-please does not manage pre-release tags. The tag must always go on a `main` commit — MinVer derives the package version from the nearest tagged ancestor, so tagging a branch commit before merging produces the wrong version.

**Process:**

1. Open a PR from your feature branch targeting `main`
2. Squash merge the PR
3. Pull `main` locally and tag the squash commit:
   ```bash
   git checkout main && git pull
   git tag v0.3.0-preview.1
   git push origin v0.3.0-preview.1
   ```
4. `publish.yml` fires automatically → CI → NuGet → GitHub Release
5. Approve the deployment in the `nuget` environment when prompted

Or use the release script which validates the preconditions for you:

```bash
bash scripts/release.sh 0.3.0-preview.1
```

### Breaking changes (major bump)

Use a `!` suffix or a `BREAKING CHANGE:` footer in your commit message:

```
feat!: remove NearbyConnectionsEvents
```

release-please will propose a major version bump in the next Release PR.

## Commit messages

This project uses [Conventional Commits](https://www.conventionalcommits.org/). Commit messages determine how the version is bumped and what appears in the changelog.

| Prefix | Effect | Example |
|---|---|---|
| `fix:` | Patch bump | `fix: handle null device in SetState` |
| `feat:` | Minor bump | `feat: add OutgoingTransferProgress event` |
| `feat!:` or `BREAKING CHANGE:` footer | Major bump | `feat!: rename SendAsync uri parameter` |
| `chore:`, `docs:`, `ci:`, `refactor:` | No bump | `docs: update iOS plist instructions` |

## Versioning

Versions are derived automatically from git tags at pack time via [MinVer](https://github.com/adamralph/minver). There is no version property in any project file — the git tag is the single source of truth.

## Running tests

### Unit tests

```bash
dotnet run --project test/Plugin.Maui.NearbyConnections.UnitTests
```

### UI tests

`test/NearbyChat.UiTests` is an Appium-driven xUnit suite that exercises the `samples/NearbyChat` sample app end-to-end (advertise/discover, connect, send/receive, disconnect) across 3 physical Android devices.

It can't be run standalone on a dev machine — it requires a live Appium server per device and expects these environment variables to already be set (normally supplied by CI):

```
DEVICE1_SERIAL, DEVICE2_SERIAL, DEVICE3_SERIAL   # adb device serials
APPIUM_1_URL, APPIUM_2_URL, APPIUM_3_URL          # Appium server URLs, one per device
APP_ACTIVITY                                      # resolved launcher activity (adb shell cmd package resolve-activity)
APP_PACKAGE                                       # optional, defaults to com.phunkeler.nearbychat
```

If `DEVICE1_SERIAL` is unset, every test in the suite skips rather than failing. In CI, the `UI Tests` workflow (`.github/workflows/ui-tests.yml`) builds the sample APK and dispatches the run to a private `android-lab` device farm that provisions the devices, installs the APK, grants permissions, and resolves the launcher activity before invoking these tests.

## Building

```bash
# All targets
dotnet build

# Specific platform
dotnet build -f net10.0-android
dotnet build -f net10.0-ios

# Pack
dotnet pack src/Plugin.Maui.NearbyConnections/Plugin.Maui.NearbyConnections.csproj -c Release
```

### Machine-local build overrides

`Directory.Build.props` holds project facts only — things true for every clone. Settings that are
true of *your machine* go in `Directory.Build.local.props` at the repo root. It is gitignored and
imported last, so its values win.

The usual reason to need one is a toolchain pin the workload manifest rejects. For example, an
Intel Mac cannot install Xcode 26.6, and the iOS workload manifest strictly rejects the 26.5 it is
stuck on:

```xml
<Project>
  <PropertyGroup>
    <ValidateXcodeVersion>false</ValidateXcodeVersion>
  </PropertyGroup>
</Project>
```

Never commit this kind of override to `Directory.Build.props` — it would silently disable the check
for every contributor and in CI.

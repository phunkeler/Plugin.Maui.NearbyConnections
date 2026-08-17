# Logging

This plugin logs through `Microsoft.Extensions.Logging`. It takes an `ILogger` from your app's DI
container and writes to whatever providers you have already configured — it does not install a
provider, write to a file, or send anything off the device.

If you configure nothing, you get the framework default: `Information` and above.

## What you see by default

Nothing, on a healthy session. Every routine event — devices appearing, connections forming,
payloads arriving — is at `Debug` or `Trace`, which the default threshold filters out.

At the default level you see only:

- **`Error`** — an operation is degraded and a consumer-visible thing failed.
- **`Warning`** — the plugin hit a problem and recovered, or dropped something it could not deliver.
- **`Information`** — one message: the iOS background teardown (see below).

That is the intended contract. If this plugin is chattering into your logs on a normal run, that is
a bug — please file it.

## The level contract

| Level | What it means | Volume |
|---|---|---|
| `Trace` | Per-payload: every message and transfer update crossing the wire. | Highest — one entry per payload |
| `Debug` | Per-device: discovery, handshakes, connection lifecycle. | One entry per device event |
| `Information` | A state change your app cannot observe any other way. | Rare |
| `Warning` | Recovered, or something was dropped. Worth reading, not necessarily acting on. | Rare |
| `Error` | An operation failed and something your app asked for did not happen. | Rare |

The one `Information` message is iOS backgrounding: MultipeerConnectivity does not survive
suspension, so the plugin tears the session down when the app backgrounds. Connections drop for a
reason that is not a defect and that your app cannot otherwise see — so it is on by default,
deliberately.

## Turning it up

All categories sit under the `Plugin.Maui.NearbyConnections` namespace, so one entry covers the
whole library:

```csharp
builder.Logging.AddFilter("Plugin.Maui.NearbyConnections", LogLevel.Debug);
```

Pick your level by what you are chasing:

- **`Debug`** — devices not appearing, connections not forming, handshakes failing. This is the
  right level for almost all troubleshooting.
- **`Trace`** — payloads not arriving, or transfers stalling. Adds one entry per payload, so expect
  real volume on an active connection.

To narrow further, filter a single category:

| Category | Covers |
|---|---|
| `Plugin.Maui.NearbyConnections.NearbyImplementation` | Session state, handshake outcomes, teardown, iOS background teardown |
| `Plugin.Maui.NearbyConnections.PlatformNearby` | The platform layer: discovery, payloads, native callbacks, and the iOS peer identity and bookkeeping messages in the 3000 range |

Neither `PeerRegistry` nor `AppLifecycleObserver` constructs its own logger, so neither has a
category of its own. `PeerRegistry` is given the platform layer's logger, so its 3000-range
messages arrive under `PlatformNearby`. `AppLifecycleObserver` is given the session's logger, so
its 3010-range messages arrive under `NearbyImplementation`. Filter either by `EventId`, not by
category.

## Event IDs

Every message carries a stable `EventId`, so you can alert on a condition without matching on
message text. Ranges are allocated per owning type:

| Range | Owner |
|---|---|
| 1000–1099 | `NearbyImplementation` — session lifecycle |
| 2000–2099 | `PlatformNearby` — the platform layer |
| 3000–3099 | iOS identity and lifecycle — `PeerRegistry` (peer keys, local peer identity, handle tracking) and `AppLifecycleObserver` |

These IDs are worth wiring an alert to:

| ID | Level | Why it matters |
|---|---|---|
| `2027` | `Error` | A platform callback threw. Carries a `{Callback}` property naming which one — one filter covers all of them. |
| `2079` | `Warning` | A payload arrived but nothing was consuming the connection, so it is being buffered and lost. Almost always a wiring bug in the app; see [PAYLOAD-DELIVERY.md](PAYLOAD-DELIVERY.md). |
| `2084` / `2085` | `Error` | An advertising or discovery start failure could not be delivered to your code. You will see a normal end-of-stream instead of the error, so this log is the only record. |
| `1013` | `Warning` | An expired inbound request could not be rejected. The device returns to `Visible` either way, but the platform may still hold the request open. |
| `1014` | `Error` | The expiry countdown for an inbound request failed. That request can stay outstanding until the session stops, so a stale row is the symptom. |

One ID you will see routinely rather than alert on: `1012` (`Debug`) records an inbound request
lapsing because the application did not answer it within
`NearbyOptions.InboundRequestTimeout`. That is an ordinary outcome, not a fault.

IDs are stable across releases and are never reused once shipped.

## Structured logging

Messages are written as templates with named properties, not interpolated strings, so a structured
sink captures the values as fields. `{DeviceId}`, `{Callback}`, and `{Writer}` are queryable rather
than baked into text — you can group every callback failure by callback name off a single event ID.

Exceptions are always passed as the exception argument, never as `ex.Message`, so your sink gets the
type and stack trace.

## Privacy

**Device display names and file paths appear in log messages**, at `Debug` for device names and at
`Error` for file paths. This is standard `ILogger` behaviour, not a defect — but display names are
user-chosen and often personal ("Sam's iPhone"), so treat them as identity data.

If those must not reach a sink — a remote/aggregated one especially — filter this library's
categories to `Warning` or above for that provider, or scrub the properties in the pipeline. Because
the values are structured properties rather than pre-formatted text, a sink-side redaction policy can
target them precisely.

## Turning it off

```csharp
builder.Logging.AddFilter("Plugin.Maui.NearbyConnections", LogLevel.None);
```

Registering no logging provider at all also works: the plugin falls back to `NullLogger` and every
call becomes a no-op.

Logging is cheap when disabled. Every message is generated by
`[LoggerMessage]`, which checks `IsEnabled` before doing any formatting work — a filtered-out
`Trace` message on the payload hot path costs a single boolean check and allocates nothing.

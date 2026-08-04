# Payload delivery

How inbound payloads reach the consumer, and why the plugin delivers them as an **async stream**
rather than as an event.

This concerns only *post-connection* data flow. Connection establishment — the
`TaskCompletionSource` handshake in `_connectionTcs`, its cancellation plumbing, and the
resolve/fault contract — is a separate mechanism and is unaffected by anything here. See
[`DEVICE-LIFECYCLE.md`](DEVICE-LIFECYCLE.md) for that.

---

## The rule

> **Lifecycle notifications are C# events. Payloads are an async stream, consumed one connection at
> a time with `await foreach`.**

Two kinds of thing with two different shapes, because they have genuinely different requirements.

| | Shape | Why |
| --- | --- | --- |
| Device found/lost, connection requested/established/dropped | `event EventHandler<T>` | Multi-consumer, handlers are fast and synchronous, no I/O |
| Inbound payloads | `IAsyncEnumerable<NearbyPayload>` | Single consumer, handlers do real async work, order matters |

```csharp
await foreach (var payload in connection.ReceiveAsync())
{
    await ProcessAsync(payload);   // may await freely; loop ends on disconnect
}
```

---

## Why payloads are a stream

### The loop body is the backpressure seam

The next payload is not dequeued until the body of your `await foreach` completes. A handler may
`await` — decode a file, generate a video thumbnail, write to a database — without losing payloads
or reordering them.

An `EventHandler<T>` returns `void` and cannot express "finish handling this one before delivering
the next." Every workaround is worse:

| Workaround | Failure |
| --- | --- |
| `async void` handler | Exceptions become unhandled crashes; ordering is lost |
| `_ = ProcessAsync(...)` fire-and-forget | Two payloads arriving back-to-back interleave; order breaks |
| Block synchronously inside the handler | Deadlock risk on the dispatcher |

This is not hypothetical. The sample's `ChatMessageService` awaits video-thumbnail generation while
handling a payload; with an event it would either crash on failure or render thumbnails against the
wrong messages.

Note that this is *sequential async consumption*, *not* throttling of the sender: the receive channel
is unbounded, so a slow consumer grows memory rather than slowing the remote peer. Backpressure in
the "make the producer wait" sense is not something this plugin provides on either shape.

### Single consumer is the honest contract

`ReceiveAsync` reads a `Channel<NearbyPayload>`. A channel reader is a *data pipe*, not a broadcast:
items read by one enumerator are permanently removed. Calling it twice throws:

```csharp
if (Interlocked.Exchange(ref _receiveGuard, 1) != 0)
{
    throw new InvalidOperationException("ReceiveAsync may only be called once per connection…");
}
```

That restriction used to be a genuine problem, because the plugin's own Tier-2 forwarding loop
claimed the single enumeration for every established connection — making a public `ReceiveAsync`
throw for any other caller, 100% of the time. **That loop no longer exists.** With
`ConnectionLifecycle.ForwardPayloadsAsync` deleted, nothing competes for the enumeration, and the
one-consumer rule is simply the documented contract.

### Fan-out belongs above the plugin, not inside it

When several components need inbound data, consume the stream once and publish an
application-level message from inside the loop. The sample does exactly this and is the model:

```csharp
await foreach (var payload in connection.ReceiveAsync())
{
    var message = await BuildChatMessageAsync(payload);   // async work, in order
    _repository.Save(device, message);
    _messenger.Send(new ChatMessageReceived(device, message));   // fan-out happens here
}
```

`ChatViewModel` is an `IRecipient<ChatMessageReceived>` — it consumes *chat messages*, not raw
payloads. This is the right layering: the plugin delivers bytes and files; the application decides
what they mean and who cares. A multi-consumer payload API would push that decision down into the
plugin, where it has no domain knowledge to make it with.

---

## Ending the loop

The receive stream completes on its own when the peer disconnects or `DisposeAsync` is called —
`CompleteReceive` completes the channel writer, `ReadAllAsync` drains whatever is still buffered, and
the loop exits normally. Payloads that arrived immediately before the disconnect are therefore
**delivered, not dropped**.

> ⚠️ **Do not pass `DisconnectedToken` to `ReceiveAsync`.** It is unnecessary — the loop already ends
> on disconnect — and actively harmful: `ReadAllAsync` observes cancellation on *every* iteration, so
> a cancelled token throws `OperationCanceledException` and discards the buffered payloads from just
> before the disconnect, which are usually the ones worth keeping.
>
> `DisconnectedToken` exists to cancel *your own* per-connection work — a retry loop, a periodic
> ping, an upload started on the peer's behalf. Pass your own token to `ReceiveAsync` only when the
> loop must stop for a reason of your own.

`NearbyConnectionTests.DisconnectedToken` pins both behaviours, including a test asserting the misuse
above still throws, so the guidance cannot silently drift from the implementation.

---

## What this costs

Stated plainly, because these are real:

| Cost | Impact |
| --- | --- |
| **One consumer per connection** | Multiple interested components require an application-level fan-out (a messenger, an event, a subject). ~3 lines, as in the sample. |
| **Manual loop management** | Someone must start the `await foreach` per connection and keep it running. Typically a single service subscribing to `ConnectionEstablished`. |
| **No LINQ-over-events ergonomics** | Not an issue in practice — the loop body is where processing goes. |

## Where events are still the right answer

Everything that is a *notification of state* rather than a *sequence of data*: devices appearing and
disappearing, connection requests arriving, connections establishing and dropping. Those have many
interested consumers, need no ordering guarantee beyond "eventually", and their handlers do no I/O.
They are plain C# events on `INearbySession`, raised on the dispatcher.

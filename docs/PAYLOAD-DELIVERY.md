# Payload delivery

How inbound payloads reach the consumer, and why the plugin delivers them as **events** rather than
as an `IAsyncEnumerable` stream.

This concerns only *post-connection* data flow. Connection establishment — the
`TaskCompletionSource` handshake in `_connectionTcs`, its cancellation plumbing, and the
resolve/fault contract — is a separate mechanism and is unaffected by anything here. See
[`DEVICE-LIFECYCLE.md`](DEVICE-LIFECYCLE.md) for that.

---

## The contract

An established `NearbyConnection` receives payloads from its remote peer. Delivery is
**multi-consumer**: any number of subscribers observe every payload, and no subscriber can consume
items out from under another.

```csharp
session.PayloadReceived += (sender, e) =>
{
    // e.Connection — the connection the payload arrived on
    // e.Payload    — NearbyBytesPayload or NearbyFilePayload
};
```

Payloads are raised in arrival order per connection. Handlers run on the plugin's dispatcher; a slow
handler delays subsequent payloads on that connection but does not block the platform SDK's callback
thread.

---

## Why not `IAsyncEnumerable`

An async stream is the more idiomatic modern-.NET shape for a sequence of arriving data, and the
plugin originally exposed one (`NearbyConnection.ReceiveAsync`). It was replaced deliberately.

### 1. The stream is single-consumer by construction

`ReceiveAsync` reads a `Channel<NearbyPayload>`. A channel reader is a *data pipe*, not a broadcast:
items read by one enumerator are permanently removed. Two consumers would each see an arbitrary
half of the payloads, so the implementation had to hard-enforce single consumption:

```csharp
if (Interlocked.Exchange(ref _receiveGuard, 1) != 0)
{
    throw new InvalidOperationException("ReceiveAsync may only be called once per connection.");
}
```

That produced a contract with no good phrasing: *"call this once — unless something else already
claimed it, in which case it throws, and you cannot find out except by calling it."*

### 2. Session-level forwarding claims the only enumeration

Once the session forwards payloads to subscribers, it *is* that single consumer. A public
`ReceiveAsync` on any real connection then throws 100% of the time — usable only against a
hand-constructed test double. A public method that cannot work for its most natural caller is worse
than no method.

The two are mutually exclusive. Either payloads fan out to subscribers, or a single consumer owns
the stream. Not both.

### 3. The backpressure argument did not survive review

The strongest case for a stream is backpressure: a slow consumer throttles the producer by awaiting
between items. **The plugin never delivered that.** Tier 2's forwarding loop was:

```csharp
await foreach (var payload in conn.ReceiveAsync(ct))
{
    _broadcaster.Publish(onPayload(conn, payload));   // TryWrite to unbounded channels
}
```

`Publish` is a non-blocking `TryWrite` onto **unbounded** channels, so the loop drained the receive
channel as fast as payloads arrived. A slow consumer caused unbounded memory growth, not
throttling — the same failure mode an event handler has.

The API advertised a property the implementation did not provide.

### 4. No consumer used the stream shape

Across the sample app and the plugin itself, the only non-test caller of `ReceiveAsync` was the
Tier-2 forwarding loop that converted it straight back into callbacks. Nothing consumed it as a
stream. Meanwhile the sample has **two independent payload consumers** (a message service and page
view models) — precisely the case a single-consumer stream cannot serve.

---

## What is genuinely lost

Stated plainly, because these are real:

| Capability | Impact |
| --- | --- |
| **Composability** | No `await foreach`, no LINQ-over-async, no `WithCancellation`. Consumers that want stream semantics must adapt the event themselves. |
| **Automatic cleanup** | A stream ends when the loop exits. An event requires `-=`; a subscriber that forgets leaks for the lifetime of the session singleton. |
| **Token-scoped consumption** | Cancellation no longer detaches a consumer implicitly. |
| **Sequential backpressure** | Not lost in practice — see §3 — but the shape that *could* have provided it is gone. |

The cleanup risk is the material one. Subscribers to a singleton must unsubscribe on teardown.

## If stream semantics are needed

An event adapts to a stream in a few lines, so nothing is permanently foreclosed:

```csharp
static async IAsyncEnumerable<NearbyPayload> AsStream(
    INearbySession session,
    [EnumeratorCancellation] CancellationToken ct = default)
{
    var channel = Channel.CreateUnbounded<NearbyPayload>();
    void Handler(object? s, NearbyPayloadReceivedEventArgs e) => channel.Writer.TryWrite(e.Payload);

    session.PayloadReceived += Handler;
    try
    {
        await foreach (var payload in channel.Reader.ReadAllAsync(ct))
        {
            yield return payload;
        }
    }
    finally
    {
        session.PayloadReceived -= Handler;
    }
}
```

The reverse — turning a single-consumer stream into a multi-consumer broadcast — requires the
broadcaster infrastructure this change removes. The event is the more primitive, more adaptable
shape.

---

## Where streams are still the right answer

This decision is specific to payload delivery. `IAsyncEnumerable` remains appropriate for:

- **Finite or terminating sequences** with a single natural consumer.
- **Genuinely backpressured pipelines**, where the consumer's rate must throttle the producer and
  the implementation actually honours it.

Neither describes inbound payload delivery to a shared session object.

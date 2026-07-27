# Backlog

Open design questions and follow-ups that came out of working sessions but weren't resolved
or implemented immediately. Tracked here (not `.building/`, which is gitignored and session-scoped)
so they survive across sessions and machines.

When picking one up: read the full entry for context, then run it through `/problem-definition`
if it's non-trivial (most of these are) before jumping to implementation.

---

## 1. Should `INearbyConnections` be `internal` instead of `public`?

**Status:** RESOLVED 2026-07-27 — **stays `public`.** See "Decision" at the end of this entry.
The "leaning toward internal" position below is preserved as the reasoning that was superseded.

**Context:** The plugin exposes two API "tiers": `INearbyConnections` (raw platform streams — always
registered by `AddNearbyConnections()`, no opt-out) and `INearbyAdvertiser`/`INearbyDiscoverer`
(opt-in convenience services built *on top of* `INearbyConnections` — they take it as a constructor
dependency). CONTRIBUTING.md justifies `INearbyConnections` being public with a "Pure DI / non-MAUI
hosts" use case.

**Finding (2026-07-24/25 session):** That justification is asserted, not demonstrated. Grepped the
full repo — no sample, no test, no consumer anywhere uses `INearbyConnections` directly outside of the
plugin's own DI wiring and test fakes. When asked directly, the answer was: "it was speculative
layering, not a real need."

**Why this matters:** Per project principles ("don't design for hypothetical future requirements,"
"simple over clever"), an unused public surface is a liability, not a feature — it's API surface
area that must be maintained, versioned, and kept stable across releases for a consumer that may
never exist. Making it `internal` now (with `INearbyAdvertiser`/`INearbyDiscoverer` as the only
public surface) can always be reversed later — going the other direction (public → internal) is a
breaking change once anyone depends on it, so the safer default pre-1.0 is to keep it closed until
a real consumer justifies opening it.

**How to apply:** If a genuine non-MAUI/console/headless consumer shows up wanting raw stream
access without Tier 2's lifecycle management, that's the trigger to make `INearbyConnections` public
again — deliberately, for a named consumer, not speculatively.

**Depends on / blocks:** Resolving this first may simplify the DI wiring in
`ServiceCollectionExtensions.cs` and could change the shape of item #2 below (the "Tier"
terminology may become moot if there's only one public tier left).

**Related option raised (2026-07-25), decide together with this item:** should
`INearbyAdvertiser`/`INearbyDiscoverer` move to a `Plugin.Maui.NearbyConnections.Extensions` namespace?
This would make the "core vs. built-on-top" relationship visible via namespace structure instead of
doc-comment prose, sidestepping the "Tier" word problem in #2 entirely. But it only makes sense
contingent on this item's outcome: if `INearbyConnections` stays public, `.Extensions` reads naturally
(root namespace = core, `.Extensions` = optional layer on top). If `INearbyConnections` goes `internal`,
there's nothing left in the root namespace for `.Extensions` to be "extending" from a consumer's
point of view — the public surface would just be `INearbyAdvertiser`/`INearbyDiscoverer` in the
root namespace, no split needed. Decide the namespace shape only after this item is resolved.

### Decision (2026-07-27): `INearbyConnections` stays `public`

**What changed the answer.** The 2026-07-24/25 finding stands on its own terms — a repo-wide grep
still shows zero direct consumers of `INearbyConnections` outside the plugin's own DI wiring, Tier 2's
constructor dependencies, and test fakes. What that grep cannot see is a consumer affordance the
codebase deliberately ships: `NearbyConnection` and `NearbyConnectionRequest` both expose **public
constructors whose XML docs state they exist "for use in test doubles of `INearbyConnections`"**
(`Connections/NearbyConnection.cs:41`, `Connections/NearbyConnectionRequest.cs:18`). Those
constructors are only meaningful if an app developer can *implement or mock the interface* in their
own test suite — which requires it to be public.

That is the named, concrete consumer the original finding said was missing. It was never
"speculative layering"; it was a real testing seam whose justification had simply been written down
wrong in CONTRIBUTING.md (as "Pure DI / non-MAUI hosts", a use case nobody could demonstrate).

**Consequences:**
- No code change. The interface, its implementation, and both test-double constructors stay public.
- CONTRIBUTING.md's rationale is replaced: the reason `INearbyConnections` is public is that
  **consumers mock it to test their own app code against the plugin**, supported by the two
  public test-double constructors. Any doc pass must state this rationale, not the old one.
- Item #2 ("Tier 1/Tier 2" terminology) is **unblocked and still open** — the public surface really
  does have two layers, so the terminology problem is real and needs its own fix. It is no longer
  waiting on this item.
- The `.Extensions` namespace option above remains viable (the "if it stays public" branch), still
  undecided, and is now decidable independently.
- Audit findings F-02 and F-07 are unblocked: docs may be written against a permanently public
  `INearbyConnections`.

**Reversal trigger:** if the test-double constructors are ever removed or the mocking seam is
replaced (e.g. by a shipped fake/test-harness package), this decision loses its basis and
`internal` should be reconsidered — deliberately, and paired with removing those constructors.

---

## 2. "Tier 1 / Tier 2" terminology cleanup

**Status:** Open — root cause identified. **Unblocked 2026-07-27:** #1 resolved as "stays public",
so the public surface genuinely has two layers and this terminology problem is real (it does not
evaporate as the "How to apply" note below speculated). Ready to fix; not yet fixed.

**Context:** "Tier 1" (`INearbyConnections`) / "Tier 2" (`INearbyAdvertiser`/`INearbyDiscoverer`) is
used inconsistently across the codebase: `Tier 1`/`Tier 2` (capitalized, CLAUDE.md and doc
comments) vs. `tier-1`/`tier-2` (lowercase, a code comment in `NearbyConnections.android.cs`, and
prose in README.md/CONTRIBUTING.md). It's never defined precisely anywhere, and it's never been a
real code-level construct (no namespace, no folder, no type literally named "Tier1" or "Tier2") —
purely documentation/comment shorthand.

**Finding (2026-07-24/25 session):** The word "Tier" implies two parallel, comparable levels you
choose between. That doesn't match the actual DI shape: `AddNearbyConnections()` *always* registers
`INearbyConnections` — there is no "Tier 1 only" registration path. `.AddAdvertiser()`/
`.AddDiscoverer()` are optional add-ons built on top of the mandatory base, not an alternative to
it. "Base service + optional decorators" is a materially different shape than "tier 1 vs tier 2,"
and the terminology confusion traces back to describing the wrong shape, not just inconsistent
casing.

**Why this matters:** A reader meeting "Tier 2" cold could reasonably read it as "lesser/advanced"
rather than "optional convenience layer on top of the required core" — the word doesn't carry the
actual relationship.

**How to apply:** Resolve #1 first. If `INearbyConnections` goes `internal`, the public API collapses
to one tier and this problem disappears on its own — no terminology fix needed. If `INearbyConnections`
stays public (a real consumer justifies it), rename away from "Tier 1/2" to something that reflects
"required core + optional add-on" (e.g. "Core API" + "Convenience API," or similar — not decided),
and fix the casing inconsistency across CLAUDE.md, README.md, CONTRIBUTING.md, and the one code
comment in `NearbyConnections.android.cs` in the same pass.

**Depends on:** #1.

---

## 3. Plugin rename — `Plugin.Maui.NearbyDevices` vs. alternatives

**Status:** Interim revert executed (2026-07-26). Final name still undecided — analysis done, do not relitigate without new information. **Naming is now locked: no rename of any kind before 1.0** except the single, final, coordinated rename described below.

**Decision (2026-07-26):** The July 2026 internal rename to `Plugin.Maui.NearbyDevices` (`4f83266`/`57c6a7e`/`0386132` — never merged to `main`, never published anywhere) was reverted top-to-bottom to `Plugin.Maui.NearbyConnections`, the plugin's only published identity: the NuGet package carrying the full 0.0.0-alpha → 0.3.0-preview.1 history, the GitHub repo, **and the preview.1 package's own public API surface** (the `v0.3.0-preview.1` tag shipped `INearbyConnections`, `UseNearbyConnections()`, etc. — the type rename happened after the tag was cut, so this revert also restores public-API continuity for anyone upgrading preview.1 → preview.2). With no explicit `<PackageId>`, the next `dotnet pack` would have silently created an orphan package; `<PackageId>Plugin.Maui.NearbyConnections</PackageId>` is now pinned in the csproj so no future project-file rename can change the published identity as a side effect. **This revert is interim-by-design and is NOT the final-name decision** — `NearbyConnections` remains disqualified as the *final* name on vendor-branding grounds (see Finding); it is simply the only identity that matches what was actually shipped.

**Finality guard (binding):** if/when a final name is chosen (candidates and criteria above remain valid), it is executed **exactly once**, as a single coordinated change spanning code, NuGet package ID (new package + deprecation/README pointer on the old), GitHub repo rename, docs, CI, and the Sonar key — and that name is then **locked through 1.0**. No interim renames, no partial renames, no "just the namespace" renames. Until that coordinated change is actually being executed, `Plugin.Maui.NearbyConnections` is the name, full stop. Any rename proposal before 1.0 that is not this one-shot coordinated change is rejected by default.

*Footnote:* audit findings F-03 and F-13 were mooted by this revert (the phantom `INearbyConnections` now exists; README badge/repo slugs match the real remote) — mooted, not "fixed"; all other audit findings remain open.

**Context:** The plugin has already been renamed once: `Plugin.Maui.NearbyConnections` →
`Plugin.Maui.NearbyDevices` (commit `4f83266`). The GitHub repo was never renamed to match — it's
still `github.com/phunkeler/Plugin.Maui.NearbyConnections` today, so a repo/package identity split
already exists in production, independent of any further rename decision. A third name,
`Plugin.Maui.NearbyConnectivity`, was raised as a candidate (echoing MAUI's built-in
`IConnectivity`).

**Finding (2026-07-24/25 session):**
- An adversarial problem-definition review (`definition-critic` + `assumptions-auditor` agents,
  see `.building/problem-definition/naming-*.md` if still present locally — gitignored, may not
  persist) concluded this is fundamentally **not a naming problem** — it's a decision-commitment
  problem. Two prior renames were driven by recurring personal doubt, not new external evidence.
  Picking a fourth name via more analysis alone is unlikely to be more "sticky" than the first two
  choices were, unless paired with an explicit finality commitment.
- Initial research favored reverting to `NearbyConnections` (matches Google's own API name,
  Flutter's closest analog `flutter_nearby_connections`, avoids the `IConnectivity` collision) —
  but this was explicitly rejected once the actual criterion was clarified: the name needs to feel
  **Microsoft/MAUI-native**, not tied to either vendor SDK (Google's "Connections" branding is
  disqualifying for this reason, not a selling point).
- MAUI's own built-in sensor APIs (`IConnectivity`, `IGeolocation`, `IAccelerometer`, etc.) are
  bare capability nouns with no vendor terms — that convention favors something like
  `NearbyConnectivity` or a fresh capability-verb name (e.g. `NearbySharing`/`NearbyTransfer`)
  over reverting to `NearbyConnections`.
- A tangent into "is the plugin's architecture too complex to feel MAUI-idiomatic" was explored and
  substantially resolved: Tier 1 (`INearbyDevices`, 3 methods) is appropriately thin; the
  complexity that exists is inherent to solving a genuinely harder problem than `IConnectivity`
  (P2P session negotiation across two divergent platform SDKs), not accidental bloat — with one
  real exception, the Tier 2 Advertiser/Discoverer duplication (see item #4, now being fixed).

**Why this matters:** A fourth rename before 1.0 is still affordable (namespaces, NuGet ID, repo
name, docs) but each additional rename compounds cost and external confusion. The critic's core
warning stands: don't just pick a fourth name and consider this closed — pair whatever is chosen
with an explicit "locked through 1.0" commitment, or expect a fifth round of doubt.

**How to apply when resumed:** Don't re-run the full research from scratch. Start from: candidates
are `NearbyConnectivity` or a fresh capability-verb name; `NearbyConnections` is disqualified
(vendor-branding collision); `NearbyDevices` (current) undersells the active/session nature of the
plugin. Resolve the repo/package identity split as part of whichever name is finally chosen,
regardless of which name wins. Note: the 2026-07-26 interim revert already resolved the
repo/package identity split for the current name — repo, NuGet ID, and code all read
`Plugin.Maui.NearbyConnections` again.

**Depends on:** Nothing blocking — can be picked up independently at any time. Not blocked by #1/#2.

---

## 4. Advertiser/Discoverer lifecycle duplication — dedup refactor

**Status:** Done (2026-07-25). Implemented, verified (two independent `build-verifier` passes,
one regression found and fixed — see below), 123/123 tests passing, zero-warning build on all
three TFMs. Not yet committed.

**Context:** `NearbyAdvertiser` and `NearbyDiscoverer` (`src/Plugin.Maui.NearbyConnections/Advertiser/`
and `.../Discoverer/`) are ~90% structurally duplicated: same fields, near-identical
`StartAsync`/`StopAsync`/`Dispose`/`DisposeAsync`, identical `MonitorConnectionAsync`/
`ForwardPayloadsAsync`/`EventsAsync` patterns — including the *exact same bug-fix comment*
(the "5+ connections accumulating" `OperationCanceledException` handling fix) copy-pasted verbatim
in both files. This is real, observed duplication with a documented incident history, not
speculative future-proofing — the "three similar lines beats a premature abstraction" principle
does not apply here.

**Decision:** Two independent proposals were generated and synthesized. Winning approach: extract
all provably-identical lifecycle/monitoring/event-fan-out code into one internal, generic composed
helper (not a base class — `AcceptAsync`/`RejectAsync` vs `ConnectAsync` have genuinely divergent
control flow, so a template-method base class would need as many strategy hooks as a composed
helper needs parameters, while also costing `sealed` on both concrete classes). Parameterized by
`TPending` (`NearbyConnectionRequest` vs `NearbyDevice`) and `TEvent` (`AdvertiserEvent` vs
`DiscovererEvent`), with divergence points (event construction, logging, running-flag) injected as
delegates. Public interfaces (`INearbyAdvertiser`/`INearbyDiscoverer`) unchanged.

**Naming — resolved 2026-07-25.** The synthesized proposal's working name, `Tier2RunLoop<TPending, TEvent>`,
was rejected on two grounds: (1) "run loop" overstates/mischaracterizes the mechanism (it's a
single `await foreach` background consumer task, not a polling/scheduling loop — the term carries
`CFRunLoop`/game-loop baggage that doesn't apply), and (2) "Tier2" bakes documentation-only,
never-before-code-level vocabulary into a permanent type identifier, premature given item #1/#2
above may eliminate the "Tier" concept entirely. Also confirmed the type is not a Strategy-pattern
implementation (no runtime swapping — each owner closes the generic once, at construction) and not
public/extendable (`internal sealed`, no virtual members) — it's narrow internal plumbing to avoid
duplicating one bug fix twice, nothing more. Checked for a BCL replacement (`BackgroundService` is
host-bound and the wrong shape; `System.Threading.Channels` fan-out is already what
`ChannelBroadcaster<T>` implements) and for external plugin precedent (Plugin.BLE just accepts the
duplication; CommunityToolkit.Maui's `MediaManager` is public API, not an internal-plumbing
analog) — neither offered a better name. Final name, matching this codebase's own
`PeerRegistry<THandle>` precedent (plain noun, generic, `sealed`, internal):
**`ConnectionLifecycle<TPending, TEvent>`**. `.building/planning/dedup-proposal.md` has been
updated to reflect this — read its "Naming (resolved 2026-07-25)" section first if resuming.

**Outcome:** `ConnectionLifecycle<TPending, TEvent>` added at
`src/Plugin.Maui.NearbyConnections/ConnectionLifecycle.cs`. `NearbyAdvertiser`/`NearbyDiscoverer`
migrated to compose it; `INearbyAdvertiser`/`INearbyDiscoverer` confirmed byte-identical
(`git diff` empty). First `build-verifier` pass caught a real regression introduced during
implementation: `EventsAsync`'s snapshot `buildSnapshot` delegate was enumerated *after* releasing
`StateLock`, and since the callers pass a lazy LINQ query closed over the live mutable snapshot
lists (matching the pre-refactor shape, which materialized via `.ToList()` *inside* the lock), a
concurrent mutation during snapshot drain could throw `InvalidOperationException` — a regression
that did not exist before. Fixed by having `ConnectionLifecycle.EventsAsync` itself materialize
`buildSnapshot()` to a `List<TEvent>` while still holding the lock, making the contract safe by
construction regardless of what the caller supplies. A second, minor divergence (the `StopAsync`
"stopped" log call firing after teardown completed instead of before, per the original code) was
also found and fixed by folding the log call into the `setRunningFlag` delegate, which
`ConnectionLifecycle.StopAsync` invokes before awaiting teardown. Both fixes confirmed by a second,
independent `build-verifier` pass (PASS). A permanent regression-guard test was added:
`test/Plugin.Maui.NearbyConnections.UnitTests/ConnectionLifecycleAdversarialTests.cs`.

**Not yet done:** commit and push. Ask before doing either.

# CLAUDE.md

@AGENTS.md

## Current work

Implementing the `DeviceState` domain model refactor. The proposal is approved and ready for
`/implementation` — do not re-run `/problem-definition`, `/design`, or `/planning` for this work.

- Direction: `.building/greenfield/design.md`
- Approved proposal + ordered steps: `.building/planning/greenfield-proposal.md`
- Sources: `.building/planning/greenfield-proposal-{a,b}.md`

Start by invoking `/implementation` against the proposal's **Ordered steps** section.

## Resolved — do not re-litigate

- **Favour DI; avoid static types.** Governs every step below. Cached singletons on `DeviceState`
  cases are dropped in favor of per-transition allocation. No new `static class` for behavior
  (extension/DI-registration classes are the sole pre-existing exception, and stay out of scope).
- **`EndReason` widens onto `NearbyConnectionChangedEventArgs`** as an additive `Reason` property —
  closes the event-time-only observability gap without touching `Ended`'s transience.
- **`Cancelled` vs `Failed`** branches on exception type per call site (`OperationCanceledException`
  → `Cancelled`, `NearbyConnectionTimeoutException` → `TimedOut`, else `Failed`) — no blanket default.
- **Android reason plumbing** is an injectable interface (mirrors `IDispatcher`/`ILogger`), resolved
  via the existing DI extension — never a static mapper.
- **Test seam stays status quo**: `DeviceState` keeps `internal set` + `InternalsVisibleTo` for the
  test project. No new injectable seam or public constructor — simplest model, no new API surface,
  YAGNI holds until a real external consumer need appears.

## Still open — resolve during implementation, not before

**The `Ended` → `Visible` double-write.** Now that `Reason` travels on the event args, check with
evidence whether writing `Ended` to `device.State` is still needed, or whether it's a redundant
`PropertyChanged` raise for a transient state no consumer can reliably observe. Decide at proposal
step 4 — don't assume either way going in.

# CLAUDE.md

Guidance for Claude Code working in this repository.

`AGENTS.md` holds the repo facts — build commands, architecture, conventions, the gotchas that
have cost real debugging time. **Read it first; it is the substance.** This file adds only what is
specific to running Claude Code here.

@AGENTS.md

## What belongs in this file

This file is committed and public. Everything in it must be actionable by any contributor who has
cloned the repo and has Claude Code — not by one maintainer on one machine.

Before adding a line here, check it against that: personal workflow preferences, user-scope skills
and slash commands, in-flight branch notes, and paths under `.claude/` all fail it and belong in
your own gitignored `.claude/CLAUDE.md` instead. A build command, an architectural invariant, or a
gotcha that will bite the next contributor passes.

CI enforces the mechanical half of this rule; see `.github/workflows/ci.yml`.

## How work flows here

Read the code, take a position, act. State the recommendation and the reason in a few lines, then
implement it — a position the maintainer can check beats a document they have to read.

Escalate to a written plan before coding only when the decision is expensive to reverse: public API
surface, published identity (see `AGENTS.md`), the platform-boundary shape, or a change touching
both platform partials at once. Say why you are escalating.

For bugs: reproduce first, then fix the root cause rather than the reported symptom. If the cause
resists two honest attempts, stop and report what was tried and what it ruled out.

## Verification is not optional

This repo has three target frameworks, warnings as errors, a build-enforced public API surface, and
a test runner that does not work the standard way. A change is not done because it compiles on one
TFM.

Before reporting work complete, run the commands in `AGENTS.md` → Commands: build all three TFMs and
run the unit tests via `dotnet run`. If a change touched a platform partial, build that platform's
TFM specifically. Report what you ran and what it returned. If something failed or you skipped a
step, say so plainly — an unverified claim of success costs more than an honest partial result.

The `PublicAPI.Unshipped.txt` baselines are part of the build. When RS0016 fires, add the listed
lines; never suppress the analyzer to go green.

## Design authority

The naming and structure contract lives in `.claude/rules/naming.md` and loads automatically when
you touch `src/Plugin.Maui.NearbyConnections/`. It is binding and outranks habit.

`DESIGN-PRINCIPLES.md` holds the reasoning behind those rules, plus the questions that are
deliberately still open — raise those, do not settle them silently.

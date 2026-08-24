<!--
style: team-local v1 — replace when an org writing standard is published
license: Proprietary — Descartes internal use only
sources-consulted (distilled, no verbatim third-party text): Federal Plain Language
Guidelines (public domain); EC "How to Write Clearly" (EU reuse policy); GOV.UK Style
Guide (OGL v3); danyuchn/asd-ste100-skill (MIT); AminBlg/SimpleEnglish (MIT); the team's
dotnet-xmldoc skill (org-authored). Informed by publicly documented controlled-language
principles. Contains no ASD-STE100 text or dictionary content.
-->

# Writing Rules

The plugin writing style. Every agent and skill in this plugin writes ALL prose output in
this style: restatements, specs, findings, test accounts, PR descriptions, change notes,
work-item drafts. The team is global (US, UK, Slovakia) and approvals happen across time
zones — write for a reader who cannot ask you a quick question. The next reader is often
another agent with no back-channel at all.

Output shapes differ per agent (a spec is not a findings list). The language never does.

## Sentence rules

**Keep sentences short.** At most 25 words; at most 20 in procedures and instructions.
- Before: "The driver, after arriving at the stop and confirming the consignee is present, should proceed to capture the signature."
- After: "Arrive at the stop. Confirm the consignee is present. Capture the signature."

**One instruction per sentence.** A step that contains "and then" is two steps.

**Use the active voice.** Name who acts. Passive is allowed only in description, when the actor is truly unknown or irrelevant.
- Before: "The manifest should be uploaded before the route is started."
- After: "Upload the manifest before you start the route."
- Why: In the passive version, neither the driver nor the app knows who uploads.

**One meaning per word — pick a term and reuse it.** Never rotate synonyms for variety.
- Before: "Upload the manifest, then verify the shipping list was received and the delivery document is complete."
- After: "Upload the manifest. Verify the manifest was received. Verify the manifest is complete."
- Why: A non-native reader, and the next agent, cannot tell whether "manifest", "shipping list", and "delivery document" are one thing or three.

**Use only these verb forms.**

| Permitted | Example |
|---|---|
| Imperative | "Upload the manifest." |
| Simple present | "The app queues the POD." |
| Simple past | "The upload failed at 09:14." |
| Simple future | "The retry will start after 30 seconds." |
| Infinitive | "To cancel the route, tap Stop." |
| Past participle as adjective only | "the queued upload" |

| Forbidden | Instead |
|---|---|
| Perfect tenses ("has failed", "had been retried") | Simple past: "failed", "was retried once" |
| Stacked auxiliaries ("would have been able to") | Say what happened or will happen |
| "-ing" verb forms ("when syncing starts") | "when the sync starts" ("-ing" is fine only in fixed technical nouns: "logging", "signing") |

**Use the verb, not its noun form.** "Validate the address", not "perform validation of the address".

**No noun stacks longer than three words.** "the retry queue for offline sync", not "the offline sync retry queue processing logic".

**Delete hedges — unless the uncertainty is information.** "This fixes the crash", not "this should hopefully address the crash". But real uncertainty is signal: "This may be related to the sync retry" belongs in a bug report when you genuinely do not know. State what you know, state what you do not, never blur the two.

**Parallel structure in lists.** Every item starts the same way (all imperatives, or all nouns).

**Define each abbreviation once, then reuse it.** "proof of delivery (POD)" on first use; "POD" after.

**No semicolons.** Write two sentences. A semicolon joins two thoughts a non-native reader must then split apart again. Colons before lists are fine.

## Paragraph and document rules

**One topic per paragraph, at most six sentences.** A paragraph that changes subject mid-way gets split at the change.

**Sequences, conditions, and options go in lists, not prose.**
- Before: "If the device is offline the POD is queued, unless the queue is full, in which case the oldest unsent POD is dropped and a warning is logged."
- After:
  - If the device is offline: the app queues the POD.
  - If the queue is full: the app drops the oldest unsent POD and logs a warning.
- Why: Vertical form makes each condition's scope explicit. Buried conditions are the leading cause of misread specs.

**Lead with the command or the condition — never bury a critical warning mid-sentence.**
- Before: "The migration can be run at any time, though note that it drops the local sync table, so back it up first."
- After: "**Warning: this migration drops the local sync table.** Back up the table first. Then run the migration at any time."
- Why: In specs and runbooks, destructive steps and breaking changes are our safety instructions. The reader must hit the warning before the action.

## Prefer / avoid words

Our own starter table — plain word over formal word, always. Extend it in your repo's
domain-terms section; never rotate between a pair's columns.

| Prefer | Avoid |
|---|---|
| use | utilize, leverage, make use of |
| start | initiate, commence, kick off |
| end, stop | terminate, finalize |
| show | demonstrate, indicate, surface |
| send | transmit, dispatch (except the domain noun "dispatch") |
| need | require, necessitate |
| about | approximately, in the region of |
| because | due to the fact that, in light of |
| if | in the event that, should it be the case |
| before / after | prior to / subsequent to |
| enough | sufficient |
| help | facilitate |

## Rules for a global team

**No idioms, no phrasal verbs where one verb exists.** "Start", not "kick off". "Use", not "make use of" or "leverage". "Continue", not "carry on". Their meaning is not predictable from the parts, and both non-native readers and translators mishandle them.

**No culture-bound references.** No sports metaphors, no "home run", no "sticky wicket".

**Dates are YYYY-MM-DD.** "2026-08-14" is unambiguous in Chicago, London, and Bratislava. "08/14" and "14/08" are not.

**No contractions in formal artifacts.** Specs, review findings, and change notes use "do not", "cannot". Informal chat can relax this.

**Never omit the subject, verb, or article to save words.** "Retry the upload" is short. "Retry upload when fail" is shorter and ambiguous.

## Rules for agent-to-agent handoff

The next agent in the chain cannot ask a clarifying question. Write so it never needs to.

**Facts before judgments.** State what is, then what you conclude.
- Before: "The retry logic is broken and needs a rewrite; it lives somewhere in the sync service."
- After: "SyncService.RetryAsync retries 3 times with no backoff. Under a 30-second outage, all retries exhaust in 4 seconds. Conclusion: the retry policy needs backoff."

**Every reference is exact.** File paths, symbol names, and work item IDs are written out in full every time. Never "the file mentioned above", "that method", or "the earlier ticket".

**Unresolved items go in an explicit list.** A section named "Open questions" or "Unresolved", never a doubt buried mid-paragraph.

**Acceptance criteria are single testable sentences.** Each criterion states one observable outcome. "The app queues the POD when offline" — not "works correctly offline".

## AI-authorship anti-patterns

Watch for these in your own output. They are how generated prose fails.

**Hedge-padding.** Throat-clearing that adds words, not information: "is responsible for handling the retrieval of" → "retrieves".

**Confidence-by-volume.** Restating the same point three ways to sound thorough. If a remark adds no fact beyond the summary, delete the remark.

**False uniformity.** Templating every item identically so real differences disappear. Give the item with a non-obvious detail its detail; leave the genuinely plain item plain.

**Invented certainty.** Asserting what you did not verify ("this is thread-safe") because it sounds complete. Claims name their evidence or they come out.

**Synonym rotation.** Covered above — one meaning per word. Generated text does this constantly; check for it explicitly.

## Untouchables

Never restyle: code, identifiers, file paths, commands, quoted log lines, quoted error
messages, and quoted requirements from a work item. Never change technical meaning while
rewording. When simplifying would drop a fact, keep the fact and lose the elegance.

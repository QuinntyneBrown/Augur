# ADR-0002: Replayed Decisions Keep Their Original Source, and the Lockfile Records Thresholds

**Date:** 2026-10-07
**Category:** integration
**Status:** Accepted
**Deciders:** Quinntyne Brown (project owner)

## Context

Three requirements pull against the replay design:

- L2-023 AC1 requires a replayed plan to be byte-identical to the first run's plan, but the plan records each decision's source, and a replayed decision's source would be `lockfile`.
- Replay re-applied thresholds to the raw answer. A decision first settled by the `default` policy or by the user has a low-confidence raw answer, so replay would ask again or fail, which breaks L2-023 AC1 and L2-037 AC1.
- `augur explain` must show "the thresholds that applied" (L2-039), but a `--min-confidence` override was not recorded anywhere.

## Decision

- The plan records a replayed decision's **original** source (`api`, `script`, `fallback`, or `user`). Only the run summary counts it as `lockfile`.
- A replayed answer carries the recorded value. The resolver checks that the value is still an accepted answer and uses it **without re-applying thresholds**.
- Each lockfile entry also stores a `thresholds` object with the thresholds that applied when it was resolved. `augur explain` prints those, and falls back to the catalog's thresholds for entries written without them.
- Raw results use the Decisions API's own answer format: `probability`; or `choice`, `confidence`, and `probabilities` as `[{ "value", "probability" }]`; or `score`, `confidence`, and `probabilities` as `[{ "value", "label", "probability" }]`; or `type: refusal`. Answer scripts use the same format. A refusal is a low-confidence answer.

## Consequences

- Replays are stable and quiet, and `explain` tells the truth after `--min-confidence`.
- A lockfile entry whose question changed is still asked again, because replay requires a matching question hash (L2-023 AC3).

## References

- `docs/specs/L2.md` L2-022, L2-023, L2-037, L2-038, L2-039
- `docs/detailed-designs/lockfile/replay-decisions/README.md`
- `docs/detailed-designs/lockfile/explain-decisions/README.md`
- ADR integration/0001

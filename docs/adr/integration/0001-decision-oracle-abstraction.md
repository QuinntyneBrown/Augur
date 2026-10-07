# ADR-0001: IDecisionOracle Abstraction with API, Scripted, and Replay Implementations

**Date:** 2026-10-07
**Category:** integration
**Status:** Accepted
**Deciders:** Quinntyne Brown (project owner)

## Context

Augur's behaviour tree asks questions whose answers come from the OpenAI Decisions API (`POST /v1/decisions`). The same tree also has to run with answers from a lockfile (L2-023, L2-037), from a scripted answer file in CI and acceptance tests (L2-038), and from `--set` overrides (L2-014). `AGENTS.md` forbids tests from calling the live API. All of these sources go through the same thresholds, closed-answer checks, and lockfile recording, so the tree must not know where an answer came from.

The Decisions API itself is in public beta with a single model (`gpt-6-luna`) and no official .NET SDK, so the HTTP client is Augur's own and has to be isolated from the rest of the system.

## Decision

A single interface, `IDecisionOracle`, answers a batch of `DecisionRequest`s for one input with a batch of `DecisionAnswer`s (raw results: probability, or choice with probabilities and confidence, or score with probabilities and confidence). Three implementations exist:

- `DecisionsApiOracle` (`Augur.Oracle.OpenAI`): typed `HttpClient`, retries, timeout, disclosure notice.
- `ScriptedOracle` (`Augur.Oracle.Scripted`): answers from a JSON file; no network.
- `ReplayingOracle` (`Augur.Core`): a decorator that serves answers from the lockfile when input and question hashes match and forwards the rest to the inner oracle.

Overrides are resolved before any oracle is consulted and never pass through the interface.

## Options Considered

### Option 1: One interface, three implementations, replay as a decorator
- **Pros:** The tree, resolver, and lockfile writer are oblivious to the answer source; the decorator makes partial replay (some from lockfile, some from API) natural; tests swap in `ScriptedOracle` via a CLI flag.
- **Cons:** One more layer to understand; the decorator has to merge answers from two sources in a defined order.

### Option 2: Branch on source inside the tree evaluator
- **Pros:** Fewer types.
- **Cons:** Every new source (or test double) changes the evaluator; replay logic entangles with traversal logic.

### Option 3: Use a general LLM client abstraction and treat Decisions as one provider
- **Pros:** Future provider swaps.
- **Cons:** Speculative; the Decisions API's typed answers (probabilities, scores) do not map onto a chat-style abstraction without losing the information thresholds depend on.

## Consequences

### Positive
- Acceptance tests run the real CLI with `--oracle-script` and never touch the network.
- Lockfile replay, offline mode, and partial refresh are variations of one decorator.

### Negative
- The `DecisionAnswer` record carries a union of three result shapes; validation has to check the shape matches the decision type.

### Risks
- If the Decisions API response schema changes during beta, only `DecisionsApiOracle` and its deserialization tests change.

## Implementation Notes

- `IDecisionOracle.AnswerAsync(SpecificationInput input, IReadOnlyList<DecisionRequest> requests, CancellationToken ct)` returns one `DecisionAnswer` per request, keyed by decision id.
- `ReplayingOracle` records which answers came from the lockfile so the run summary can report `lockfile` as a source.
- `--offline` wraps the inner oracle with one that throws for any forwarded request.

## References

- `docs/specs/L2.md` L2-014 to L2-018, L2-023, L2-037, L2-038, L2-041 to L2-043
- `docs/detailed-designs/oracle/query-decisions-api/README.md`
- OpenAI Decisions API guide: https://developers.openai.com/api/docs/guides/decisions

## Project Overview

Augur is a C# code generator that uses the OpenAI Decisions API (`POST /v1/decisions`) to decide *what* to generate, then emits code deterministically. The model never writes code; it only picks from a closed set of options the generator supports.

### How it works

```
spec / prompt / image ──► Behaviour tree (Decisions API at fuzzy nodes)
                              │
                              ▼
                     GenerationPlan (typed, serializable)
                              │
                              ▼
                     Deterministic emitters (templates / Roslyn) ──► code
```

- **Behaviour tree** drives the decision process. Decisions API question types map to node types:
  - `predicate` → condition node (probability vs. threshold → Success/Failure)
  - `choice` → oracle selector (model picks which child to run)
  - `score` → utility node (score drives a decision, e.g. use CQRS if complexity ≥ 2)
  - Deterministic conditions (file exists, CLI flags) stay plain C# leaves; only ambiguous decisions call the API.
- **GenerationPlan** is the contract between deciding and emitting. It can be inspected, diffed, hand-edited, and replayed.
- **Emitters** turn the plan into code with no model involvement.

### Design principles

- **Reproducible:** every decision is recorded in a lockfile (`decisions.json`, keyed by input hash + question). Re-runs replay recorded answers and only re-query when the spec changes.
- **Low-confidence handling:** every oracle node has a fallback (safe default, interactive prompt, or hard failure) below a confidence threshold.
- **Testable:** the API sits behind `IDecisionOracle`; tests run trees against a fake oracle with scripted answers.
- **Batch siblings:** independent questions on the same input go in one request; dependent decisions are sequential (one request per tree level).
- **Treat option descriptions like code:** choice/level descriptions drive accuracy, so they are versioned and tested.

### Notes

- The Decisions API is in public beta; `gpt-6-luna` is the only model.
- No official .NET SDK support is documented yet; call the endpoint with a typed `HttpClient`.
- Images must be inline base64 data URLs.

## Incremental Implementation and ATDD - mandatory

Every new feature or change to production behavior MUST have a requirement and a
detailed design before implementation begins. This includes behavioral changes
to existing features, such as changing a command's options, output, or the code
it generates. This requirement applies to production-code changes only;
documentation-only and test-only changes are out of scope unless they are part
of implementing a production behavior change.

Every production behavior implementation MUST invoke and follow the
`incremental-implementation` skill (`.claude/skills/incremental-implementation`
and `.agents/skills/incremental-implementation`) before any code is written,
combined with acceptance test-driven development (ATDD). Plan small,
reviewable slices, then complete one slice at a time: write Given-When-Then
acceptance criteria, write the acceptance test, and run it to prove it fails for
the expected reason BEFORE writing production code. Implement only what satisfies
that slice, refactor with tests green, and run the relevant regression checks.
Do not move to the next slice until those checks pass. No bulk implementation,
no tests added afterward, and no weakening tests to manufacture a pass. Keep
the requirement, detailed design, criteria, tests, and implementation aligned
until the entire feature or behavior change is complete.

Acceptance tests exercise the CLI the way a user does: arguments and input files
in; exit code, stdout/stderr, and the generated files out. Assert on observable
results - the emitted code, the `GenerationPlan`, and `decisions.json` - not on
internals. Tests never call the live Decisions API: run them against a fake
`IDecisionOracle` with scripted answers, including low-confidence answers to
cover fallbacks.

### Never write architecture tests

Never add a test that asserts the shape of the codebase rather than its behavior:
no structure, layout, or naming tests; no banned-API scans; no traceability tests
that parse the specifications. Those constraints belong to the compiler, the
formatter, and review. A test suite exists to prove behavior.

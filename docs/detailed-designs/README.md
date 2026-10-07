# Augur detailed designs

Each folder below holds one vertically sliced feature design: a `README.md` with the feature's background, components, level-2 requirements, and diagrams, plus a `diagrams/` folder with the PlantUML sources and rendered images. The requirements the designs refine live in [`../specs/L1.md`](../specs/L1.md) and [`../specs/L2.md`](../specs/L2.md). Architecture decisions the designs rely on live in [`../adr/`](../adr/).

| Subsystem | Feature | L2 requirements |
|-----------|---------|-----------------|
| `cli` | [run-command](cli/run-command/README.md) | L2-001, L2-002, L2-003, L2-004, L2-040 |
| `cli` | [install-tool](cli/install-tool/README.md) | L2-049, L2-050 |
| `intake` | [read-specification](intake/read-specification/README.md) | L2-005, L2-006 |
| `catalog` | [define-catalog](catalog/define-catalog/README.md) | L2-007, L2-009 |
| `catalog` | [list-catalog](catalog/list-catalog/README.md) | L2-008 |
| `decisions` | [evaluate-decision-tree](decisions/evaluate-decision-tree/README.md) | L2-010, L2-047 |
| `decisions` | [resolve-decision](decisions/resolve-decision/README.md) | L2-011, L2-012, L2-013, L2-015 |
| `decisions` | [override-decision](decisions/override-decision/README.md) | L2-014 |
| `decisions` | [handle-low-confidence](decisions/handle-low-confidence/README.md) | L2-019, L2-020, L2-021 |
| `oracle` | [query-decisions-api](oracle/query-decisions-api/README.md) | L2-016, L2-017, L2-018, L2-041, L2-042, L2-043 |
| `oracle` | [script-answers](oracle/script-answers/README.md) | L2-038 |
| `lockfile` | [record-decisions](lockfile/record-decisions/README.md) | L2-022, L2-025 |
| `lockfile` | [replay-decisions](lockfile/replay-decisions/README.md) | L2-023, L2-024, L2-037 |
| `lockfile` | [explain-decisions](lockfile/explain-decisions/README.md) | L2-039 |
| `plan` | [write-plan](plan/write-plan/README.md) | L2-026, L2-027 |
| `plan` | [validate-plan](plan/validate-plan/README.md) | L2-028, L2-029 |
| `emission` | [emit-output](emission/emit-output/README.md) | L2-032, L2-033, L2-034, L2-035, L2-044, L2-045, L2-058 |
| `emission` | [generate-end-to-end](emission/generate-end-to-end/README.md) | L2-036, L2-048 |
| `emission` | [emit-dotnet-solution](emission/emit-dotnet-solution/README.md) | L2-030, L2-031, L2-046 |
| `emission` | [emit-angular-workspace](emission/emit-angular-workspace/README.md) | L2-031, L2-051, L2-052, L2-054, L2-055, L2-056, L2-057 |
| `emission` | [wire-fullstack](emission/wire-fullstack/README.md) | L2-053 |

`L2-031` (emitted code builds and passes its tests) appears under both emitter features because it constrains both outputs.

## Reading order

A reader new to Augur should start with `decisions/evaluate-decision-tree`, which explains the behaviour tree that every other feature serves, then follow a run in order: `intake` → `catalog` → `decisions` → `oracle` → `lockfile` → `plan` → `emission`. The `cli` designs describe the command shell around that run.

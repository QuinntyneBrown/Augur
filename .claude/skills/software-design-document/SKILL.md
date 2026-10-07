---
name: software-design-document
description: >-
  Generate detailed software design documents from an existing L1/L2 requirements
  set. Use this skill whenever the user wants to produce design docs, detailed
  designs, a software design document (SDD), architecture documentation, or
  per-feature / per-subsystem design write-ups — especially when they mention a
  docs/specs folder, L1/L2 requirements, C4 diagrams, PlantUML, sequence or class
  diagrams, vertical slices, or a docs/detailed-designs folder. Trigger this even
  when the user only says "document the design", "write up the architecture",
  "turn these requirements into designs", or "create the detailed designs"
  without naming the format. The skill scaffolds
  docs/detailed-designs/{subsystem}/{feature}/ with a README written in a strict
  house style, plus rendered C4, class, and sequence diagrams shown inline.
---

# Software design document

This skill turns a set of requirements into a browsable tree of detailed feature
designs. The output is deterministic in shape, so a reader always knows where to
look:

```
docs/detailed-designs/
└── {subsystem}/
    └── {feature}/
        ├── README.md          detailed design (renders on GitHub, images inline)
        └── diagrams/
            ├── *.puml          PlantUML sources
            └── *.png           rendered images the README links to
```

The design is written **against requirements** — every feature traces back to
level-2 (L2) requirements, which in turn refine level-1 (L1) requirements. That
traceability is the reason for the requirements gate below: without L1/L2
requirements there is nothing to design against, and inventing them would defeat
the purpose.

Read the three references as you reach the steps that need them:
- `references/writing-style.md` — the house voice every README uses.
- `references/diagrams.md` — PlantUML templates for C4, class, and sequence.
- `references/example-design.md` — a complete finished feature to imitate.

---

## Step 0 — Requirements gate (do this first, every time)

A detailed design is a refinement of requirements. If the requirements are not
present, stop and ask for them rather than fabricating a design that traces to
nothing.

Check for a `docs/specs/` folder (relative to the repo root) that contains
markdown holding **both** L1 and L2 requirements. Detect this by searching the
markdown for requirement identifiers at both levels. The level token (`L1`/`L2`)
and the domain appear in **either order**, and a single spec set may **mix** the
two conventions — treat both as valid:

- level-first: `L1-<DOMAIN>-<n>`, `L2-<DOMAIN>-<n>` — e.g. `L1-INTAKE-001`, `L2-CONSENT-002`
- domain-first: `<DOMAIN>-L1-<n>`, `<DOMAIN>-L2-<n>` — e.g. `INTAKE-L1-001`, `CONSENT-L2-002`

Sections or tables clearly labelled L1 and L2 also count. This regex catches both
orderings:

```bash
ls docs/specs 2>/dev/null && grep -rroE "(L[12]-[A-Z]+|[A-Z]+-L[12])-[0-9]+" docs/specs | sort -u | head
```

When you read an identifier, parse the **level** (does it contain `L1` or `L2`?)
and the **domain** (the alphabetic segment on the other side of the level token)
independently of their order — `L2-INTAKE-002` and `INTAKE-L2-002` denote the
same level and domain. Keep each identifier **exactly as the spec writes it**;
never rewrite one convention into the other.

**If `docs/specs/` is missing, has no markdown, or shows no recognizable L1 and
L2 requirements: stop and tell the user.** Say plainly what is needed — a
`docs/specs/` folder of markdown requirements at levels L1 and L2, each with an
identifier and L2 tracing to its parent L1 — and do not scaffold anything or
invent requirements. This is the one hard stop in the workflow.

## Step 1 — Identify the subsystems

Think of the system as a small number of subsystems (bounded contexts / major
capability areas).

- **If `docs/specs/` already has one folder per subsystem, reuse those names
  exactly.** The specs are the source of truth for naming; matching them keeps
  the two trees aligned and lets a reader move between spec and design without
  translating names.
- **Otherwise, infer the subsystems from the specs.** The domain segment of the
  requirement IDs is a strong signal (`INTAKE`, `CONSENT`, `NFR`, …) — it is the
  alphabetic segment of the identifier, on whichever side of the `L1`/`L2` token
  it sits (`L2-INTAKE-002` and `INTAKE-L2-002` both have domain `INTAKE`).
  Headings and grouping in the spec markdown are further signals. Group related
  requirements into named subsystems.

## Step 2 — Slice each subsystem into vertical features

Within a subsystem, split the work into **vertically sliced features**. A
vertical slice is one user-facing capability that cuts through every layer it
needs — UI → API → application/domain → data — rather than a horizontal layer
shared across capabilities. "Decline an application" is a vertical slice;
"the repository layer" is not.

Good slices map cleanly onto one or a few L2 requirements and can be described,
diagrammed, and reasoned about on their own. Name each slice as a short verb
phrase (`decline-application`, `record-consent`, `submit-application`).

## Step 3 — Scaffold the folders

For each subsystem and each of its features, create:

```
docs/detailed-designs/{subsystem}/{feature}/diagrams/
```

Use the subsystem naming from Step 1 and kebab-case feature names from Step 2.

## Step 4 — Write each feature's README.md

The detailed-design file **is** the feature folder's `README.md` — GitHub renders
it automatically and shows the diagram images inline. Use exactly these four
headings, in this order, and write all prose in the house voice
(`references/writing-style.md`). `references/example-design.md` shows a complete,
finished target — imitate its shape.

### Overview
Background, assuming the reader knows nothing. Explain what the feature is, why
it exists, where it sits in the subsystem, and define every local domain term at
first use. A newcomer should be able to read only this section and understand
what the feature is for.

### Description
The concrete building blocks of the slice: the components, types, functions, and
classes involved, and what each one does. If the codebase already contains these,
inspect the source and describe what is really there, using the real names. If
the feature is greenfield, describe the components the design introduces. Where a
detail is genuinely undecided, mark it `<TO SUPPLY>` rather than guessing.

### Requirements
List **only L2 requirements** — no L1 specs stand on their own here. Present them
as a table of the L2 identifier, the L1 identifier it refines, and the
requirement text:

```markdown
| L2 ID | Refines (L1) | Requirement |
|-------|--------------|-------------|
| `L2-INTAKE-002` | `L1-INTAKE-001` | The system shall record a decline decision with a reason. |
```

Pull the exact text and IDs from `docs/specs/`; keep requirement wording in the
`shall/should/may` form. Copy each identifier **verbatim** in whatever convention
the spec uses — if the specs write `INTAKE-L2-002` / `INTAKE-L1-001`, the table
cells read `INTAKE-L2-002` and `INTAKE-L1-001`, not a reordered form. Consistency
with the source of truth matters more than a uniform house ordering, so that
every id in the design still resolves back to a real line in the specs.

### Diagrams
Embed the rendered diagrams with a one- or two-sentence prose lead-in before each
(the prose introduces and interprets the picture; no significant fact should live
only inside an image). Reference the **`.png`**, with a relative path, so it shows
inline:

```markdown
![C4 component view for declining an application](diagrams/c4-component.png)
```

Include, per feature: the C4 diagrams (context, container, component), a class
diagram (structure + relationships), and a sequence diagram per behaviour.

## Step 5 — Author the diagrams

Write a `.puml` per diagram into the feature's `diagrams/` folder, following
`references/diagrams.md`. The essentials:

- **C4** uses the offline standard library: `!include <C4/C4_Context>`,
  `<C4/C4_Container>`, `<C4/C4_Component>`. Build the diagram **from the C4 macros
  that include defines** — `Person(...)`, `System(...)`, `Container(...)`,
  `Component(...)`, the `*_Boundary(...) { }` blocks, and `Rel(...)` for every
  relationship. Never draw raw `rectangle`/`component` shapes or bare `a --> b`
  arrows inside a C4 diagram; that discards the styling and semantics the include
  exists to give. Scope each diagram to what the feature touches.
- **Class** diagrams show fields, methods, and typed relationships (association,
  dependency, inheritance, composition).
- **Sequence** diagrams show behaviour with `box` groupings that separate
  **Frontend** from **Backend** and name the specific application/layer, coloured
  by tier. The sequence template in `references/diagrams.md` is the gold standard;
  match its skin and box structure. Trace steps to requirement IDs in the message
  text where a step enforces one.

## Step 6 — Render every diagram to PNG

The README links to `.png` files, so each `.puml` must have a rendered sibling.
Render the whole tree with the bundled script (it locates `plantuml.jar` or
`plantuml` on PATH and writes each `.png` next to its source). C4 and class
diagrams also need Graphviz (`dot`); the script checks for it first and exits 2
if it is missing, because PlantUML would otherwise write an error image and
still report success:

```bash
python <skill>/scripts/render_puml.py docs/detailed-designs
```

A non-zero exit means at least one diagram failed to render — an image the README
points at does not exist. Read the reported error, fix the `.puml`, and re-run
until the exit is clean. (Equivalent direct call if needed, after `java -jar $PLANTUML_JAR -testdot`
reports "Installation seems OK":
`java -jar $PLANTUML_JAR -tpng docs/detailed-designs/**/diagrams/*.puml`.)

## Step 7 — Verify before finishing

- **Every `.puml` has a `.png`** — the render step exited clean.
- **Every image link resolves** — each `![...](diagrams/x.png)` in a README
  points to a file that exists.
- **C4 diagrams use the C4 macros** — each C4 `.puml` includes a `<C4/...>`
  header and is composed of `Person`/`System`/`Container`/`Component`/`Rel` (and
  boundary) macro calls, with no raw `rectangle`/`component`/`node` shapes or bare
  arrows standing in for them.
- **Requirements are L2-only**, each with its L1 parent and exact text from the
  specs.
- **Prose passes the house-style self-check** in `references/writing-style.md`
  (no `we`/`you`, obligation only via `shall/should/may`, no hype or fillers,
  gaps marked `<TO SUPPLY>` not invented).

## Quality bar

A finished design tree lets a reader open any feature folder on GitHub and see a
self-contained page: plain-language background, the concrete parts, the L2
requirements it satisfies, and diagrams that render inline — all in one steady
voice, every claim tracing to a requirement or marked as an open question.

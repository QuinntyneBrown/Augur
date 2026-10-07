# Diagrams — PlantUML templates and conventions

Every diagram is a PlantUML `.puml` source in the feature's `diagrams/` folder,
rendered to a sibling `.png` by `scripts/render_puml.py`. The README embeds the
`.png` (never the `.puml`) so the picture shows inline on GitHub.

Three diagram kinds appear in every feature design, each answering a different
question:

| Kind | Answers | PlantUML basis |
|------|---------|----------------|
| **C4** | Where does this feature sit — who uses it, what deploys it, what parts make it up? | `!include <C4/...>` |
| **Class** | What is the static structure — the types, their fields/methods, and how they relate? | UML class diagram |
| **Sequence** | What happens at run time — the ordered messages for each behaviour? | UML sequence, with frontend/backend boxes |

Keep diagrams **scoped to the feature slice**. A feature-level C4 component
diagram shows the handful of components this slice touches, not the whole
system. Breadth belongs at the subsystem level; a feature diagram that tries to
show everything becomes unreadable.

---

## C4 diagrams

C4-PlantUML ships inside `plantuml.jar`, so `!include <C4/...>` resolves
**offline** — no internet, no URL includes. Use the bracket-include form the
standard library expects:

- `!include <C4/C4_Context>` — System Context (people + systems)
- `!include <C4/C4_Container>` — Containers (apps, APIs, databases)
- `!include <C4/C4_Component>` — Components (classes/handlers inside a container)

**Build the diagram from the C4 macros the include defines — this is the whole
point of the include.** Once a `.puml` includes a C4 header, every element must
be a C4 macro call: `Person(...)`, `System(...)`, `Container(...)`,
`Component(...)`, `ContainerDb(...)`, the `System_Boundary(...) { }` /
`Container_Boundary(...) { }` blocks, and `Rel(...)` for every relationship. Do
**not** fall back to raw PlantUML shapes (`rectangle`, `component`, `node`,
`[Foo]`, `-->` between bare aliases) inside a C4 diagram — those render as plain
boxes and throw away the C4 styling, legend, and semantics the include exists to
provide. If you find yourself drawing a bare `rectangle`, reach for the matching
C4 macro instead.

Including `<C4/C4_Component>` transitively pulls in the Container and Context
macros too, so a single `!include <C4/C4_Component>` gives you the full macro set
(`Person`, `System`, `Container`, `Component`, boundaries, `Rel`) for all three
diagrams — you do not need three different includes if you prefer one.

Produce, per feature, the C4 levels that carry information for the slice:
a **Context**, a **Container**, and a **Component** diagram. If two levels would
be near-identical for a tiny slice, keep the more detailed one and say so in the
prose rather than padding.

### C4 Context

```plantuml
@startuml
!include <C4/C4_Context>
LAYOUT_WITH_LEGEND()
title System Context — Decline an application

Person(admin, "Program administrator", "Reviews and decides youth applications")
System(wordup, "WordUp", "Intake and enrollment platform")
System_Ext(email, "Email provider", "Sends applicant notifications")

Rel(admin, wordup, "Decides applications using")
Rel(wordup, email, "Sends decline notice via", "SMTP")
@enduml
```

### C4 Container

```plantuml
@startuml
!include <C4/C4_Container>
LAYOUT_WITH_LEGEND()
title Containers — Decline an application

Person(admin, "Program administrator", "Decides applications")
System_Boundary(wordup, "WordUp") {
  Container(spa, "Portal", "Angular", "Administration UI")
  Container(api, "WordUp API", "ASP.NET Core", "Application and domain logic")
  ContainerDb(db, "WordUp database", "SQL Server", "Applications and audit trail")
}
Rel(admin, spa, "Uses", "HTTPS")
Rel(spa, api, "Calls", "JSON/HTTPS")
Rel(api, db, "Reads and writes", "EF Core")
@enduml
```

### C4 Component

```plantuml
@startuml
!include <C4/C4_Component>
LAYOUT_WITH_LEGEND()
title Components — Decline an application

Container_Boundary(api, "WordUp API") {
  Component(ctrl, "ApplicationsController", "ASP.NET Core controller", "Exposes /applications endpoints")
  Component(handler, "DeclineApplicationCommandHandler", "MediatR handler", "Applies the decline decision")
  Component(entity, "YouthApplication", "Domain entity", "Owns the application state machine")
  ComponentDb(db, "WordUp database", "SQL Server", "Persists application state")
}
Rel(ctrl, handler, "Sends DeclineApplicationCommand")
Rel(handler, entity, "Loads and mutates")
Rel(handler, db, "Persists", "EF Core")
@enduml
```

The C4 macro vocabulary to compose from — use these, not raw UML shapes:

| Purpose | Macros |
|---------|--------|
| People | `Person(alias, "label", "desc")`, `Person_Ext(...)` |
| Systems | `System(...)`, `System_Ext(...)`, `SystemDb(...)`, `SystemQueue(...)` |
| Containers | `Container(...)`, `ContainerDb(...)`, `ContainerQueue(...)` |
| Components | `Component(...)`, `ComponentDb(...)`, `ComponentQueue(...)` |
| Grouping | `System_Boundary(alias, "label") { ... }`, `Container_Boundary(...) { ... }`, `Enterprise_Boundary(...) { ... }` |
| Relationships | `Rel(from, to, "label", "tech")`, `Rel_Back(...)`, `BiRel(...)`, directional `Rel_D/U/L/R(...)` |
| Layout | `LAYOUT_WITH_LEGEND()`, `LAYOUT_TOP_DOWN()`, `LAYOUT_LEFT_RIGHT()` |

Every relationship is a `Rel(...)` call, never a bare `a --> b` arrow — the
`Rel` macro is what draws the labelled, styled C4 connector.

---

## Class diagrams

Show both **structure** (fields and methods, with types) and **relationships**.
Prefer the real types from the codebase when the code exists; otherwise model the
types the design introduces. Apply the shared skin header (below) so class and
sequence diagrams look like one set.

```plantuml
@startuml
' --- shared skin header ---
skinparam backgroundColor #FFFFFF
skinparam shadowing false
skinparam roundcorner 8
skinparam defaultFontName Arial
skinparam ArrowColor #344054
skinparam class {
  BorderColor #344054
  FontColor #101828
  BackgroundColor #F9FAFB
}
' --------------------------
title Class structure — Decline an application

enum ApplicationStatus {
  Submitted
  Declined
  Enrolled
}

class YouthApplication {
  +Guid Id
  +ApplicationStatus Status
  +string DeclineReason
  +Decline(reason: string): void
}

class DeclineApplicationCommand {
  +Guid ApplicationId
  +string Reason
}

class DeclineApplicationCommandHandler {
  +Handle(command: DeclineApplicationCommand): Task<Result>
}

DeclineApplicationCommandHandler ..> DeclineApplicationCommand : handles
DeclineApplicationCommandHandler --> YouthApplication : loads / mutates
YouthApplication --> ApplicationStatus : has
@enduml
```

Relationship arrows, so the reader can read intent from the line:

| Meaning | Arrow |
|---------|-------|
| Association ("has a") | `-->` |
| Dependency / uses | `..>` |
| Inheritance / implements | `<|--` (`..|>` for interface realization) |
| Composition (owns lifetime) | `*--` |
| Aggregation (references) | `o--` |

Add multiplicities where they matter: `YouthApplication "1" --> "*" ConsentRecord`.

---

## Sequence diagrams

One sequence per behaviour of the slice (the happy path, plus significant
alternates such as a validation failure). Sequence diagrams are where the
**frontend/backend separation and application context** must be visible.

PlantUML `box ... end box` groups participants into a shaded band. Use one box
per architectural home, and label each box with **both** the tier
(*Frontend* / *Backend*) **and** the specific application or layer it belongs to
— that labeling is how "which application" context is shown. Colour the boxes by
tier so the eye can separate frontend from backend at a glance:

| Box | Colour |
|-----|--------|
| `Frontend — <app>` | `#E0F2FE` |
| `Backend — <API app>` | `#D1FADF` |
| `Backend — <Application and Domain>` | `#ECFDF3` |
| `Backend — <Infrastructure>` | `#FFF4E5` |

This template is the gold standard — match its shape, skin, and box structure:

```plantuml
@startuml
title UML sequence behaviour — Decline an application
skinparam backgroundColor #FFFFFF
skinparam shadowing false
skinparam roundcorner 12
skinparam defaultFontName Arial
skinparam ArrowColor #344054
skinparam rectangle {
  BorderColor #344054
  FontColor #101828
}
skinparam package {
  BorderColor #667085
  FontColor #101828
  BackgroundColor #F9FAFB
}
actor "Program administrator" as actor
box "Frontend — Portal application" #E0F2FE
  participant "ApplicationsList" as page
  participant "ApplicationsApi" as client
end box
box "Backend — WordUp API application" #D1FADF
  participant "ApplicationsController" as controller
end box
box "Backend — WordUp Application and Domain" #ECFDF3
  participant "DeclineApplicationCommandHandler" as handler
  participant "YouthApplication" as entity
end box
box "Backend — WordUp Infrastructure" #FFF4E5
  database "WordUp database" as db
end box
actor -> page : Start decline an application
page -> client : Build typed request
client -> controller : HTTPS request
controller -> controller : Authenticate and apply endpoint policy
controller -> handler : Send DeclineApplicationCommand
handler -> db : Load authorized state
db --> handler : Current state
handler -> entity : Apply L2-INTAKE-002 decline transition
entity --> handler : Valid state transition
handler -> db : Save changes in one unit of work
db --> handler : Commit result
handler --> controller : Typed result
controller --> client : HTTP response or ProblemDetails
client --> page : Feature result
page --> actor : Present decline result
@enduml
```

Notes that keep sequences honest:
- Trace to requirements in the message text where a step enforces one, e.g.
  "Apply `L2-INTAKE-002` decline transition." This ties behaviour to the
  Requirements section.
- `->` for a call, `-->` for a return. Keep the return arrows; they show where
  control comes back.
- Model error/alternate flows with `alt` / `else` / `opt` when the slice has
  them, rather than a second near-duplicate diagram.

---

## Rendering

After writing or editing any `.puml`, render the whole tree:

```bash
python <skill>/scripts/render_puml.py docs/detailed-designs
```

The script finds `plantuml.jar` (or `plantuml` on PATH), refuses to run without
Graphviz `dot` (C4 and class diagrams would otherwise render as a "Cannot find
Graphviz" error image), writes each `.png` next to its `.puml`, and exits
non-zero if any diagram fails — so a red exit means an
image the README links to does not exist yet. Fix the source and re-run until it
is clean.

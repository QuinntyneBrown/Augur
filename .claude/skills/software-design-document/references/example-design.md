# Worked example — a finished feature design

This is what one feature's `README.md` should look like when complete. It lives
at `docs/detailed-designs/intake/decline-application/README.md`, and its
`diagrams/` sibling holds the `.puml` sources plus the rendered `.png` files it
links to. Study the shape, the four headings, the L2-only requirements table,
the inline image links, and the house voice — then produce the same for each
real feature.

Everything below the line is the file content.

---

# Decline an application

## Overview

WordUp is an intake and enrollment platform for a youth program. A *youth
application* is a request from a young person to join the program; it moves
through a small set of states from submission to a final decision. This feature
covers one decision an administrator can make: declining an application.

A program administrator reviews a submitted application and, when the applicant
does not meet the program criteria, records a decline together with a reason.
Declining is a deliberate state change, not a deletion: the application and its
reason are retained for audit and for later reporting on decision outcomes. Once
declined, an application is terminal and accepts no further transitions.

This document assumes no prior knowledge of WordUp's internals. The terms used
below are defined at first use, and the diagrams show where each part lives.

## Description

The feature is a vertical slice that runs from the administration UI to the
database.

- **`ApplicationsList`** — Angular page component in the Portal application. It
  lists submitted applications and offers the decline action.
- **`ApplicationsApi`** — typed Angular HTTP client. It builds the request for
  the decline endpoint and returns a typed result to the page.
- **`ApplicationsController`** — ASP.NET Core controller in the WordUp API. It
  exposes the `/applications` endpoints, authenticates the caller, applies the
  endpoint policy, and dispatches the command.
- **`DeclineApplicationCommand`** — the request object carrying the target
  `ApplicationId` and the `Reason`.
- **`DeclineApplicationCommandHandler`** — MediatR handler holding the
  application logic. It loads the authorized application, applies the decline,
  and persists the change in one unit of work.
- **`YouthApplication`** — domain entity that owns the application state machine.
  Its `Decline(reason)` method enforces the transition rules.
- **`ApplicationStatus`** — enumeration of the states an application may hold:
  `Submitted`, `Declined`, `Enrolled`.

## Requirements

The feature realizes the following level-2 (L2) requirements. Each L2
requirement refines a level-1 (L1) requirement, cited by identifier.

| L2 ID | Refines (L1) | Requirement |
|-------|--------------|-------------|
| `L2-INTAKE-002` | `L1-INTAKE-001` | The system shall record a decline decision against a submitted application together with a reason. |
| `L2-INTAKE-003` | `L1-INTAKE-001` | A declined application shall be terminal and shall accept no further state transitions. |
| `L2-CONSENT-002` | `L1-CONSENT-001` | The system shall retain a declined application and its reason for audit. |
| `L2-NFR-006` | `L1-NFR-002` | The decline endpoint shall authorize the caller before any state is read or written. |

## Diagrams

### System context

The administrator decides applications through WordUp, which notifies applicants
through an external email provider.

![C4 system context for declining an application](diagrams/c4-context.png)

### Containers

The decision travels from the Portal single-page application to the WordUp API,
which persists state in the WordUp database.

![C4 container view for declining an application](diagrams/c4-container.png)

### Components

Inside the WordUp API, the controller dispatches a command to the handler, which
mutates the `YouthApplication` entity and persists it.

![C4 component view for declining an application](diagrams/c4-component.png)

### Class structure

`DeclineApplicationCommandHandler` depends on `DeclineApplicationCommand`, loads
and mutates `YouthApplication`, and the entity holds an `ApplicationStatus`.

![Class diagram for declining an application](diagrams/class-structure.png)

### Behaviour — decline an application

The controller authenticates and applies its endpoint policy, then dispatches
the command; the handler applies `L2-INTAKE-002` on the entity and commits in one
unit of work.

![Sequence diagram for declining an application](diagrams/sequence-decline.png)

---

## What the diagrams/ folder holds

For the README above, `diagrams/` contains five `.puml` sources and their five
rendered `.png` files:

```
decline-application/
├── README.md
└── diagrams/
    ├── c4-context.puml        c4-context.png
    ├── c4-container.puml      c4-container.png
    ├── c4-component.puml      c4-component.png
    ├── class-structure.puml   class-structure.png
    └── sequence-decline.puml  sequence-decline.png
```

The `.puml` sources follow the templates in `references/diagrams.md`. Image
links are **relative** (`diagrams/c4-context.png`) so GitHub renders them inline
when the folder's README is viewed.

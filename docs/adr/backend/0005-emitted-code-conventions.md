# ADR-0005: Conventions for Emitted Code

**Date:** 2026-10-07
**Category:** backend
**Status:** Accepted
**Deciders:** Quinntyne Brown (project owner)

## Context

The emission designs left several choices open: what sample feature the emitted code contains, how test projects avoid needing a database, which SDK builds the output, and how the fullstack API serves the Angular app. Emitted code may depend only on validated plan values and the solution name (L2-044), so any sample content has to be fixed in the templates.

## Decision

- **Sample feature.** Every emitted solution contains a small `Notes` feature: a `Note` entity, `GET` and `POST /api/notes`, an in-memory store or an EF Core `NotesDbContext`, and command and query handlers when CQRS is on. It shows the chosen architecture with real code and gives the tests something to exercise. Nothing connects to a database at startup, and no connection string contains a password.
- **Tests without a database.** The emitted API tests always check `/api/health`. With authentication they check that notes return 401 and that the API refuses to start outside Development without an authority. Without authentication, and with in-memory or SQLite storage, they create and list notes, using a temporary SQLite file.
- **SDK.** Emitted solutions include a `global.json` that selects .NET 10 or a later stable SDK and never a preview, so a preview SDK on the machine cannot change the build.
- **Warnings.** Emitted projects treat warnings as errors, except NuGet audit warnings (NU1901 to NU1904), so an advisory published after release cannot break a build. Advisories are checked by the release audit instead (L2-046 AC2).
- **Development ports.** The API's launch profile and the Angular proxy use ports derived from the solution name, the same on every machine.
- **Fullstack serving.** The API serves the published app with static files and a fallback route constrained with `nonfile`, so requests for bundles reach the static file middleware and only client routes return the index page. Unknown `/api` paths return 404. With server-side rendering the index page is `index.csr.html`.

## Consequences

- Golden copies in `tests/Augur.Tests/Golden` pin these conventions.
- Removing the sample feature is the first thing a user does after generating; it is self-contained in one folder or layer.

## References

- `docs/specs/L2.md` L2-030, L2-031, L2-044, L2-046, L2-053
- `docs/detailed-designs/emission/emit-dotnet-solution/README.md`
- `docs/detailed-designs/emission/wire-fullstack/README.md`

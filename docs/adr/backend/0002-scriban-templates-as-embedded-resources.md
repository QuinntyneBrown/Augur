# ADR-0002: Scriban Templates as Embedded Resources, No External Tools at Emit Time

**Date:** 2026-10-07
**Category:** backend
**Status:** Accepted
**Deciders:** Quinntyne Brown (project owner)

## Context

Augur emits .NET solutions, Angular workspaces, and fullstack combinations from a validated `GenerationPlan`. The requirements make emission deterministic (L2-032: byte-identical output for the same plan on any machine), forbid any external process or network access during emission (L2-058: no `dotnet`, `npm`, `node`, `ng`), and require that emitted content, including `package-lock.json`, come from Augur itself. The emitter also has to keep every emitted file free of model-controlled or specification text (L2-044); only validated plan values and the solution name may influence output.

The obvious shortcut, shelling out to `dotnet new` and `ng new`, would make output depend on whatever SDK versions are installed and would break determinism and offline operation.

## Decision

Emitters render Scriban templates that are compiled into the emitter assemblies as embedded resources. Each decision value selects a template group; template inputs are limited to the typed `GenerationPlan` and names derived from it. Every file an emitted project needs, including lock files, is a template or a static embedded resource. Augur never starts a child process during emission.

## Options Considered

### Option 1: Scriban templates as embedded resources
- **Pros:** Pure .NET, fast, no runtime dependency on external tools; templates are versioned with Augur, so the same Augur version always emits the same bytes; Scriban's sandboxed model prevents templates from reading the environment.
- **Cons:** Templates must be kept current with Angular and .NET releases by hand; `package-lock.json` has to be regenerated and re-embedded when Angular packages change.

### Option 2: Shell out to `dotnet new` and `ng new`, then patch
- **Pros:** Scaffolds track the latest SDK templates automatically.
- **Cons:** Violates L2-058 and L2-032; output depends on installed SDK versions and network access; requires Node.js on the user's machine.

### Option 3: Roslyn syntax trees for C# and string builders for everything else
- **Pros:** Compile-time safety for C# output.
- **Cons:** Roslyn cannot express `.csproj`, `angular.json`, TypeScript, or HTML; two emission mechanisms would coexist; much slower to author.

## Consequences

### Positive
- Determinism and offline emission follow directly from the design.
- The test suite can diff emitted output against golden files.

### Negative
- Updating Angular or .NET versions is a deliberate release task: regenerate templates and lock files, run the full build matrix (L2-031), ship a new Augur version.

### Risks
- Template drift: a template that compiles today may warn tomorrow under a newer SDK. The release pipeline builds every `dotnet` and `angular` combination and a pairwise `fullstack` set, which catches drift before release.

## Implementation Notes

- Templates live under `Templates/{feature}/` in each emitter project and are marked `EmbeddedResource`.
- Template context exposes only `Plan`, `SolutionName`, and `AngularProjectName`.
- Static files (lock files, `.editorconfig`, icons) are embedded as-is and copied byte-for-byte.
- Emitted text is normalized to UTF-8 without BOM and LF line endings before writing.

## References

- `docs/specs/L2.md` L2-030 to L2-032, L2-044, L2-052, L2-058
- ADR-0003 (staging-directory atomic emission)

# ADR-0003: Staging-Directory Atomic Emission

**Date:** 2026-10-07
**Category:** backend
**Status:** Accepted
**Deciders:** Quinntyne Brown (project owner)

## Context

Emission writes dozens to hundreds of files into a directory the user owns. The requirements say a failed or cancelled emission leaves the output directory unchanged (L2-034, L2-004), that `--force` overwrites only the files Augur emits (L2-033), that no write may escape the output directory or pass through a symbolic link (L2-045), and that `--dry-run` lists exactly what a real run would write (L2-035). Writing files directly into the output directory as they render cannot satisfy the first of these: a failure halfway through leaves a partial tree.

## Decision

Emitters render every file into an in-memory `FileSet`, then write the set to a staging directory created next to the output directory (same volume). Only after every file is rendered and the containment check passes does Augur move the staged files into the output directory. If any step fails, the staging directory is deleted and the output directory is untouched.

## Options Considered

### Option 1: Render to a FileSet, stage on the same volume, then move
- **Pros:** All-or-nothing from the user's point of view; the `FileSet` doubles as the `--dry-run` listing; containment is checked once over the full path list before anything touches the output directory.
- **Cons:** Needs free space for a second copy during the move; a hard kill during the final move can still leave a partially moved tree (bounded to the move phase only).

### Option 2: Write directly to the output directory with a rollback journal
- **Pros:** No staging copy.
- **Cons:** Rollback has to restore overwritten files, which means reading and buffering originals; more failure modes than it removes.

### Option 3: Write to a temporary directory, then rename the whole directory
- **Pros:** True single-operation atomicity on most file systems.
- **Cons:** Incompatible with `--force` into a non-empty directory that holds user files Augur must preserve; cross-volume renames fail.

## Consequences

### Positive
- `--dry-run`, `--force`, containment, and atomicity all derive from the same `FileSet` abstraction.
- Fault-injection tests (L2-034) have one seam: fail one file render and assert the output directory is unchanged.

### Negative
- Peak disk use during emission is about twice the emitted size.

### Risks
- The final move phase is not atomic across many files. It is short (a rename per file on the same volume) and happens only after every validation has passed, which keeps the window small.

## Implementation Notes

- `StagingDirectory` is created as a sibling of the output directory with a random suffix and removed in a `finally` block.
- `OutputDirectoryGuard` resolves every target path and rejects any that leave the output root or pass through a reparse point.
- Per-file moves use `File.Move(overwrite: true)` only when `--force` is set.

## References

- `docs/specs/L2.md` L2-004, L2-033 to L2-035, L2-045
- ADR-0002 (templates as embedded resources)

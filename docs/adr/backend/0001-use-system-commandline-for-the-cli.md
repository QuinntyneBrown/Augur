# ADR-0001: Use System.CommandLine for the CLI

**Date:** 2026-10-07
**Category:** backend
**Status:** Accepted
**Deciders:** Quinntyne Brown (project owner)

## Context

Augur is a .NET global tool with six subcommands (`plan`, `emit`, `generate`, `explain`, `catalog`, `schema plan`), a shared set of options, strict exit codes, and a hard rule that machine-readable output goes to stdout while diagnostics go to stderr (L2-001 to L2-004). Acceptance tests drive the CLI exactly as a user does, so argument parsing, help text, and error reporting are part of the tested contract, not incidental plumbing. The parser also has to support `--spec -` for stdin, repeatable options (`--set`, `--image`, `--refresh`), and validation that returns exit code 2 with a one-line `error:` message.

## Decision

Augur uses `System.CommandLine` for argument parsing, help generation, and command dispatch. One command class per verb lives in `Augur.Cli`; option validation that depends on the catalog (for example, `--set` values) runs in a validation step before the handler executes, so invalid usage never reaches the core.

## Options Considered

### Option 1: System.CommandLine
- **Pros:** Microsoft-maintained; generates `--help` with option defaults; typed option binding; built-in support for repeatable options, response files, and cancellation tokens; trimming-friendly for a global tool.
- **Cons:** API has changed across pre-release versions; help output format is not fully customizable; some validation has to be written by hand.

### Option 2: Spectre.Console.Cli
- **Pros:** Rich help and console rendering; settings classes map cleanly to commands.
- **Cons:** Rich rendering pulls in a larger dependency for a tool that writes mostly plain text; its default output conventions blur the stdout/stderr separation that L2-003 requires.

### Option 3: Hand-written parser
- **Pros:** No dependency; total control over every message.
- **Cons:** Every feature of a real parser (help, defaults, repeatables, typos) has to be built and tested by hand; high cost for no requirement-driven benefit.

## Consequences

### Positive
- Help text and option defaults come from one declaration, which keeps L2-001 criterion 2 true without duplicated strings.
- Cancellation (L2-004) uses the parser's built-in `CancellationToken` plumbing.

### Negative
- Error messages produced by the parser itself need a thin adapter so every failure is a single `error:` line on stderr with exit code 2.

### Risks
- A future major version of `System.CommandLine` could change the API. The command layer is thin, so the migration cost is bounded to `Augur.Cli`.

## Implementation Notes

- Register one `Command` per verb; share common options (`--verbosity`, `--lock`) through a single options class.
- Map parser errors to `ExitCode.InvalidUsage` (2) in one place.
- Never let the parser write help or errors to stdout.

## References

- `docs/specs/L2.md` L2-001 to L2-004
- `docs/detailed-designs/cli/run-command/README.md`

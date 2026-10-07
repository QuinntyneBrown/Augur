# Contributing to Augur

Thank you for your interest in contributing to Augur. This document explains
how to report issues, propose changes and get a pull request merged.

By participating in this project, you agree to abide by the
[Code of Conduct](CODE_OF_CONDUCT.md).

## Ways to contribute

- **Report a bug.** Search the [existing issues](https://github.com/QuinntyneBrown/Augur/issues)
  first. If you do not find a match, open a new issue with the Augur version
  (`augur --version`), your operating system, the command you ran, the output
  you expected and the output you got. Run with `--verbosity diagnostic` and
  include the relevant stderr output. Remove any API keys and private
  specification text first.
- **Suggest a feature.** Open an issue that describes the problem you are
  trying to solve before proposing a solution. Changes to the decision catalog
  or to generated code are design decisions and are discussed in an issue
  first.
- **Improve the documentation.** Fixes to the README, specifications and other
  documentation are always welcome.
- **Submit code.** See the workflow below.

Security vulnerabilities must not be reported in public issues. Follow
[SECURITY.md](SECURITY.md) instead.

## Development setup

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
2. Fork the repository and clone your fork:

   ```shell
   git clone https://github.com/<your-username>/Augur.git
   cd Augur
   ```

3. Build and run the tests:

   ```shell
   dotnet build
   dotnet test
   ```

The test suite does not call the Decisions API and does not need an API key.

### The slow suite

`tests/Augur.IntegrationTests` builds and tests the code Augur emits, drives the
emitted Angular apps in a headless browser, audits their dependencies, and
installs the packed tool. It needs Node.js 22 and runs only when asked:

```shell
AUGUR_RUN_SLOW=1 dotnet test tests/Augur.IntegrationTests
```

It builds a pairwise-covering set of plans by default; set
`AUGUR_FULL_MATRIX=1` to build every combination. Set `AUGUR_KEEP_BUILDS=1` to
keep the emitted code for inspection.

### Changing templates

- Golden copies of emitted code live in `tests/Augur.Tests/Golden`. After an
  intended template change, run `AUGUR_UPDATE_GOLDEN=1 dotnet test` and review
  the diff before committing.
- Emitted Angular workspaces ship one of 16 embedded `package-lock.json` files.
  When you change an npm version or dependency in
  `src/Augur.Emission.Angular/AngularModel.cs`, regenerate them with
  `pwsh eng/Regenerate-AngularLockfiles.ps1` and run the slow suite.
- Changing a decision's instructions, options, descriptions, or thresholds
  changes its question hash. Bump the catalog version and update
  `tests/Augur.Tests/Catalog/question-hashes.v2.json` as the failing test
  explains.

### Release checklist

Before tagging a release:

1. Run the full slow suite: `AUGUR_RUN_SLOW=1 AUGUR_FULL_MATRIX=1 dotnet test tests/Augur.IntegrationTests`.
   It builds and tests every emitted combination and runs the npm and NuGet vulnerability audits.
2. Check Core Web Vitals by hand (L2-057): emit an `angular` plan with
   `server-side-rendering=false`, run `npm ci` and `npm run build`, serve
   `dist/<name>/browser`, and run Lighthouse with mobile emulation against the
   home page. Largest Contentful Paint must be at most 2.5 s and Cumulative
   Layout Shift at most 0.1.
3. Update `CHANGELOG.md` and the `<Version>` in `Directory.Build.props`.

## Development workflow

Augur follows requirements-first, acceptance test-driven development. The full
rules are in [AGENTS.md](AGENTS.md); in short:

1. **Start from a requirement.** Every change to production behavior needs a
   requirement in [`docs/specs`](docs/specs) and a detailed design before code
   is written. Documentation-only and test-only changes do not.
2. **Work in small slices.** Split the change into small, reviewable slices and
   finish one before starting the next.
3. **Write the test first.** For each slice, write Given-When-Then acceptance
   criteria and an acceptance test, and run the test to confirm it fails for
   the expected reason. Then write only the code needed to make it pass.
4. **Test behavior, not structure.** Acceptance tests run the CLI the way a
   user does and assert on exit codes, output and generated files. Tests use a
   fake `IDecisionOracle` with scripted answers and never call the live API. Do
   not add tests that check the shape of the codebase, such as naming or layout
   rules.

## Design principles

Keep these in mind when changing Augur:

- The model chooses; it never writes code. Text returned by the model, other
  than a validated answer, must never reach a generated file.
- The same plan always produces the same files.
- Every decision has a safe default and a confidence threshold.
- Option and level descriptions in the decision catalog affect accuracy. Treat
  them like code: change them on purpose, version them and test them.

## Pull requests

1. Create a branch from `main`.
2. Make your change, following the workflow above.
3. Make sure `dotnet build` and `dotnet test` pass with no new warnings.
4. Update the documentation and [CHANGELOG.md](CHANGELOG.md) when behavior
   visible to users changes.
5. Open a pull request against `main`. Describe what changed and why, and link
   the issue and the requirements it implements.

Keep each pull request focused on one change. A maintainer will review it and
may ask for changes before merging.

### Commit messages

Write commit messages in the imperative mood, with a short summary line of 72
characters or fewer, for example `Add --dry-run to the emit command`. Use the
body to explain why the change was made.

## Licensing

Augur is licensed under the [MIT License](LICENSE). By submitting a
contribution, you agree that it is licensed under the same terms.

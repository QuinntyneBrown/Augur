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

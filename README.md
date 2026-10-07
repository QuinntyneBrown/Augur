# Augur

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Platforms](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-lightgrey)](#supported-platforms)

Augur is a command-line code generator for .NET and Angular. It uses the
[OpenAI Decisions API](https://platform.openai.com/docs) to decide *what* to
generate from a natural-language specification, then emits the code
deterministically. The model never writes code. It only chooses from a closed
set of options that Augur supports, and every choice is recorded so that runs
are reproducible.

> [!IMPORTANT]
> Augur is pre-release. Every command described here works when built from
> source, but no package has been published to NuGet yet, and file formats may
> change before 1.0. The full behavior is specified in [`docs/specs`](docs/specs).

## Contents

- [Why Augur](#why-augur)
- [How it works](#how-it-works)
- [Getting started](#getting-started)
- [Usage](#usage)
- [What Augur generates](#what-augur-generates)
- [Configuration](#configuration)
- [Decision catalog](#decision-catalog)
- [Data and privacy](#data-and-privacy)
- [Documentation](#documentation)
- [Contributing](#contributing)
- [Code of conduct](#code-of-conduct)
- [Security](#security)
- [Support](#support)
- [License](#license)

## Why Augur

Language models are good at reading intent and bad at producing the same output
twice. Code generators are the opposite. Augur keeps each side doing what it is
good at:

- **The model decides, the generator writes.** The model picks answers such as
  "clean architecture", "PostgreSQL" or "uses authentication". Templates turn
  those answers into code. No model text is ever written into a generated file.
- **Reproducible by default.** Every decision is recorded in a lockfile
  (`decisions.json`). Re-running the same specification replays the recorded
  answers without calling the API, and only changed decisions are asked again.
- **Inspectable.** Decisions are collected into a `GenerationPlan`: a versioned
  JSON document that you can review, diff, edit by hand and replay.
- **Safe when the model is unsure.** Every decision has a confidence threshold
  and a safe default. Below the threshold, Augur uses the default, asks you, or
  stops, according to the policy you choose.
- **CI friendly.** Offline mode and scripted answer files let Augur run in
  pipelines and tests without network access.

## How it works

```text
specification (text + images) ──► behaviour tree ──► GenerationPlan ──► emitters ──► code
                                    │                (JSON, editable)    (templates)
                                    └─ Decisions API, only at ambiguous nodes
```

1. **Decide.** Augur walks a behaviour tree built from its decision catalog.
   Deterministic checks run as plain code. Only ambiguous decisions are sent to
   the Decisions API, and independent questions are batched into one request.
2. **Plan.** The resolved answers are written to a `GenerationPlan`, which is
   the only input to code emission.
3. **Emit.** Emitters render the plan into a .NET solution, an Angular
   workspace or a fullstack application. The same plan always produces the same
   files.

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- An OpenAI API key with access to the Decisions API. A key is needed only
  when a decision has to be sent to the API; replayed, offline and scripted
  runs do not need one.

### Install from source

Augur is a .NET tool. Until it is published to NuGet, pack and install it from
a clone:

```shell
git clone https://github.com/QuinntyneBrown/Augur.git
cd Augur
dotnet pack src/Augur.Cli -c Release -o ./artifacts
dotnet tool install --global Augur.Cli --add-source ./artifacts
augur --version
```

Once it is published, `dotnet tool install --global Augur.Cli` is enough.

### Build and test

```shell
dotnet build
dotnet test
```

`dotnet test` runs the fast suite. The slow suite builds and tests the code
Augur emits, drives it in a browser, and installs the packed tool; it needs
Node.js 22 and runs only when asked:

```shell
AUGUR_RUN_SLOW=1 dotnet test tests/Augur.IntegrationTests
```

### Quick start

Describe what you want in a Markdown file, then generate:

```shell
export OPENAI_API_KEY="<your-key>"   # PowerShell: $env:OPENAI_API_KEY = "<your-key>"

augur generate --spec spec.md --name Contoso.Orders --out ./Contoso.Orders
```

This resolves the decisions, writes `decisions.json` and
`./Contoso.Orders/augur.plan.json`, and emits the code into `./Contoso.Orders`.
Run the same command again and the decisions are replayed from the lockfile
with no API calls.

## Usage

| Command | Description |
|---------|-------------|
| `augur plan` | Resolve decisions for a specification and write a `GenerationPlan`. |
| `augur emit` | Emit code from an existing `GenerationPlan`. |
| `augur generate` | Run `plan` and then `emit` in one step. |
| `augur explain` | Show how each decision in a lockfile was reached. |
| `augur catalog` | List the decisions Augur can make and their allowed answers. |
| `augur schema plan` | Print the JSON Schema for `GenerationPlan`. |

Run `augur <command> --help` to see every option for a command.

### Plan, review, then emit

```shell
# Resolve decisions and save the plan for review
augur plan --spec spec.md --image wireframe.png --name Contoso.Orders --out plan.json

# See why each decision was made
augur explain

# Preview the files that would be written, then emit
augur emit --plan plan.json --out ./Contoso.Orders --dry-run
augur emit --plan plan.json --out ./Contoso.Orders
```

### Common options

| Option | Description |
|--------|-------------|
| `--spec <path>` | Specification file (UTF-8, up to 256 KiB). Use `-` to read from stdin. |
| `--image <path>` | PNG, JPEG or WEBP image to send with the specification (up to 4, 10 MiB each). |
| `--name <SolutionName>` | Name of the generated solution, for example `Contoso.Orders`. |
| `--out <path>` | Output file for `plan`, or output directory for `emit` and `generate`. |
| `--set <id>=<value>` | Fix a decision instead of asking the model. Repeatable. |
| `--lock <path>` | Lockfile location. Defaults to `./decisions.json`. |
| `--refresh [<id>]` | Ignore recorded decisions (all, or only the named ones) and ask again. |
| `--on-low-confidence <default\|prompt\|fail>` | What to do when the model is not confident enough. |
| `--offline` | Forbid network access and resolve decisions only from overrides and the lockfile. |
| `--oracle-script <path>` | Take answers from a JSON file instead of the Decisions API. |
| `--force` | Allow emitting into a non-empty directory. Only Augur's own files are overwritten. |
| `--dry-run` | List the files that would be written without writing anything. |
| `--verbosity <quiet\|normal\|detailed\|diagnostic>` | Control diagnostic output on stderr. |

### Running in CI

Commit `decisions.json` and run offline so builds never depend on the model:

```shell
augur generate --spec spec.md --name Contoso.Orders --out ./out --offline
```

For tests, supply scripted answers instead of calling the API:

```shell
augur plan --spec spec.md --name Contoso.Orders --oracle-script answers.json
```

### Exit codes

Machine-readable output goes to stdout. Progress, warnings and errors go to
stderr.

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | Unexpected internal error |
| 2 | Invalid usage or invalid input |
| 3 | One or more decisions could not be resolved |
| 4 | Decisions API failure |
| 5 | Output conflict or file-system write failure |
| 130 | Cancelled by the user |

## What Augur generates

| Target | Output |
|--------|--------|
| `dotnet` | A .NET 10 solution (`{Name}.slnx`) with an API in the chosen architecture, optional EF Core persistence, JWT bearer authentication, a background worker, and an xUnit test project. |
| `angular` | An Angular 21 workspace with standalone components, an accessible and responsive app shell, optional Angular Material, signals or an NgRx signal store, optional server-side rendering, and OpenID Connect sign-in with PKCE. |
| `fullstack` | Both: the .NET solution at the root and the Angular workspace in `web/`. In development the Angular server proxies `/api`; `dotnet publish` builds the Angular app into the API's `wwwroot`, so it deploys as one unit. |

Every emitted file depends only on the plan's decisions and the solution name;
no text from the specification or the model is copied into code. Package
versions are pinned exactly, and emitted Angular workspaces include a
`package-lock.json`.

## Configuration

| Environment variable | Description |
|----------------------|-------------|
| `OPENAI_API_KEY` | API key for the Decisions API. Augur reads the key only from this variable; there is no command-line option for it. |
| `AUGUR_OPENAI_BASE_URL` | Overrides the API base URL. HTTPS is required, except for loopback hosts used by local test servers. |

## Decision catalog

Augur can only make the decisions defined in its built-in, versioned catalog.
Run `augur catalog` to see the catalog installed with your version.

| Decision | Type | Answers |
|----------|------|---------|
| `target` | choice | `fullstack`, `dotnet`, `angular` |
| `authentication` | predicate | `true`, `false` |
| `architecture` | choice | `clean-architecture`, `vertical-slice`, `minimal-api` |
| `persistence` | choice | `ef-core-sqlserver`, `ef-core-postgresql`, `ef-core-sqlite`, `none` |
| `background-processing` | predicate | `true`, `false` |
| `domain-complexity` | score | Enables CQRS when the domain scores 2 or higher on a 0–3 scale |
| `ui-library` | choice | `angular-material`, `none` |
| `state-management` | choice | `signals`, `ngrx-signal-store` |
| `server-side-rendering` | predicate | `true`, `false` |

Decisions that do not apply to the chosen target are skipped. For example, a
`dotnet` target never asks about `ui-library`.

## Data and privacy

When a decision has to be sent to the Decisions API, Augur sends the
specification text and any images you pass with `--image` to OpenAI. Before the
first request in a run, Augur prints the endpoint host and the names and sizes
of the files it will send. Replayed, offline and scripted runs send nothing.

Augur never writes the specification text, image data or API key to the
lockfile, the plan, logs or generated files.

## Documentation

- [High-level requirements (L1)](docs/specs/L1.md)
- [Detailed requirements and acceptance criteria (L2)](docs/specs/L2.md)
- [Contributor and agent guidelines](AGENTS.md)
- [Changelog](CHANGELOG.md)

## Supported platforms

Augur runs on Windows (x64, arm64), Linux (x64, arm64) and macOS (arm64).

## Contributing

Contributions are welcome. Please read [CONTRIBUTING.md](CONTRIBUTING.md) for
how to propose changes, the development workflow and the testing requirements.

## Code of conduct

This project has adopted the [Contributor Covenant](CODE_OF_CONDUCT.md). By
participating, you are expected to uphold it.

## Security

Please do not report security vulnerabilities through public GitHub issues.
See [SECURITY.md](SECURITY.md) for how to report them privately.

## Support

See [SUPPORT.md](SUPPORT.md) for how to get help and file issues.

## License

Augur is licensed under the [MIT License](LICENSE).

## Trademarks

OpenAI is a trademark of OpenAI. .NET is a trademark of Microsoft Corporation.
Angular is a trademark of Google LLC. Augur is not affiliated with or endorsed
by any of these companies.

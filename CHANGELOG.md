# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- The `augur` .NET tool with `plan`, `emit`, `generate`, `explain`, `catalog`,
  and `schema plan` commands.
- Decision catalog version 2 with nine decisions, question hashes, and a
  snapshot test that requires a version bump when a question changes.
- Decisions from the OpenAI Decisions API (batched by dependency level, with
  retries, timeouts, and a data disclosure notice), from answer scripts, from
  `--set` overrides, and from the `decisions.json` lockfile, with
  `--refresh` and `--offline`.
- Low-confidence policies: use the default, prompt in a terminal, or fail.
- Atomic, contained emission of .NET 10 solutions, Angular 21 workspaces, and
  fullstack applications, with `--force` and `--dry-run`.
- A fast acceptance suite and a slow suite that builds, tests, audits, and
  browser-checks emitted code, plus a CI workflow.
- High-level (L1) and detailed (L2) requirements in `docs/specs`.
- Project documentation: README, contributing guide, code of conduct, security
  policy, support policy and MIT license.

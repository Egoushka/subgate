# 0004. The engine is a library with no I/O

- Status: accepted (owner, 2026-10-29)
- Date: 2026-10-29

## Context

The pace and quota logic is the part nobody else has (prior art in architecture.md: none gives per-consumer weekly shares or pacing). Other programs should be able to use it without subgate's service: an orchestrator's own admission logic, a simulation, tests, and a proxy transport if one ever comes back (ADR 0010).

## Decision

Components, dependencies pointing down only:

| Component | Job | I/O |
|---|---|---|
| engine | Windows, pace line, states, attribution (ADR 0005), leases, decisions. Pure functions over `Observation`s (a meter reading, a usage report, a lease event) and a `Policy`; the clock is a parameter. | none |
| provider | Per subscription type: parse its meter readings and usage reports into `Observation`s, map its rate-limit kinds to windows. Anthropic first; OpenAI and Codex plans later. | parses data, opens nothing |
| store | Counters per consumer per window, k, last meter, leases, overrides, audit. In-memory for tests, SQLite for deployments (ADR 0009). | yes |
| policy | Defaults from a YAML file, overlaid by runtime overrides from the store. | yes |
| api | The HTTP API (ADR 0007), `/metrics`. | yes |
| cli | `serve`, `run`, `sample`, `statusline` (ADR 0012), `token` (ADR 0006). | yes |

The engine has a property test and a simulation test (the attribution target, ADR 0005) that run without a network or a disk.

**Where the engine lives.** In switchknife, the author's public MIT .NET libraries, as `Switchknife.Pace`: its own NuGet version (tag `pace/v<x>`), no dependencies beyond the base library, AOT-clean, following switchknife's package rules. subgate references the package; the other components live in this repository. Until the first `pace/v0.1.0` release, subgate may reference the project from a local switchknife checkout.

## Consequences

The service is a thin shell; most tests are engine tests. A provider adapter is the only code a new subscription type needs.

## Reopen if

The engine's API churns enough that a separate release cadence costs more than it gives.

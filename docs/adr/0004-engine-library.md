# 0004. The engine is a library with no I/O

- Status: proposed
- Date: 2026-10-28

## Context

The pace and quota logic is the part nobody else has (prior art: ccflare, anthropic-proxy, LiteLLM pass-through; none gives per-consumer weekly shares or pace gating). Other programs should be able to use it without the proxy: an orchestrator's admission check, a CLI, a test simulation, ledger mode (ADR 0010).

## Decision

One Go module, these packages, dependencies pointing down only:

| Package | Job | I/O |
|---|---|---|
| `engine` | Pace line, states, attribution (ADR 0005), decisions. Pure functions over `Observation`s (a limit snapshot, a usage event) and a `Policy`; the clock is a parameter. | none, no dependencies beyond the standard library |
| `provider` | `Provider` interface: classify a request, parse limit headers into window snapshots, tap a response body for usage, rewrite a request for the `api`/`cheap` actions. `provider/anthropic` first; OpenAI and Codex later behind the same interface. | parses bytes, opens nothing |
| `store` | `Store` interface: counters per consumer per window, calibration factor k, last snapshot, overrides, audit log. `store/memory` first, then `store/sqlite`; Postgres if a second instance ever shares state. | yes |
| `policy` | `Source` interface: defaults from a YAML file, overlaid by runtime overrides from the store. | yes |
| `proxy` | HTTP transport: `httputil.ReverseProxy`, the streaming tap, actions. | yes |
| `api` | Control API (`/v1/status`, `/v1/consumers`, approvals) and `/metrics`. | yes |
| `cmd/subgate` | Wiring and config. | yes |

`engine` has a fuzz test and a simulation test (the attribution target, ADR 0005) that run without a network or a disk.

## Options considered for sharing

- **switchknife** (the author's shared libraries): it is .NET-only, with Native AOT rules per package. A Go engine doesn't fit. A .NET port is worth it only when a .NET program must run the engine in-process; today the orchestrator reads subgate's API instead.
- **A separate Go module per package**: more release overhead for no consumer yet. Split `engine` into its own module (`github.com/Egoushka/subgate/engine`, own tag) when a second program imports it.

## Consequences

The proxy is a thin shell; most tests are engine tests. A provider adapter is the only code a new subscription type needs.

## Reopen if

A second importer appears (split the module), or a .NET consumer needs the engine in-process (port to switchknife).

# 0010. Ledger first; the proxy is off the roadmap

- Status: accepted (owner, 2026-10-29). Replaces an earlier version of this ADR, accepted the same day, that put the proxy first with the ledger as a fallback.
- Date: 2026-10-29

## Context

The engine (ADR 0004) can take its observations from a proxy's response tap or from clients' reports. The Usage Policy line effective 2026-11-12 (legal.md, claim 3) makes the proxy's status UNKNOWN; the ledger routes nothing (ADR 0003).

## Decision

- v0.1 to v0.3 ship the ledger only: the HTTP API (ADR 0007), the client CLI, the signal sampler and the status-line reporter (ADR 0012).
- The proxy transport is not on the roadmap. The engine stays transport-neutral, taking `Observation`s from any source, so a proxy can be added later without touching the engine.

## Consequences

The spike's proxy code and its latency numbers stay as evidence (ADR 0002) but ship nothing.

## Reopen if

Anthropic clarifies that a single-owner, self-hosted proxy is permitted.

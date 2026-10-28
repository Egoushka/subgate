# Status

Phase 0 (design), 2026-10-28. Worker and session ids are kept in the owner's private notes, not here.

## Decisions

| Date | Decision | By |
|---|---|---|
| 2026-10-28 | Public repository from day one, presented as self-hosted, single-owner software | owner |
| 2026-10-28 | The owner's interactive Claude Code goes through subgate, measured, never gated, failing open | owner |
| 2026-10-28 | Default shares and actions are set in the private deployment config, not in this repo | owner |
| 2026-10-28 | ADRs 0001–0011 proposed | lead |

## Waiting on the owner

1. Read the Usage Policy line effective 2026-11-12 (legal.md, claim 3) and decide: go ahead with the proxy, or start from the ledger.
2. Name `subgate` (ADR 0001).
3. Approve the v0.1 spec (specs/v0.1.md), including the fail-over layers (ADR 0008).
4. CI on GitHub-hosted runners in this repo instead of the private shared workflows (ADR 0011).
5. Whether the plan's monthly API credits are linked to a Console organisation, and which Max tier: changes the `api` action's economics (v0.2).

## Running

- Language benchmark, Go vs Rust vs .NET 10 Native AOT: fills ADR 0002.

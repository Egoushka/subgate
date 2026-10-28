# Status

Phase 0 (design). Worker and session ids are kept in the owner's private notes, not here.

## Decisions

| Date | Decision | By |
|---|---|---|
| 2026-10-28 | Public repository from day one, presented as self-hosted, single-owner software | owner |
| 2026-10-28 | The owner's interactive Claude Code is measured, never gated, and never depends on subgate | owner |
| 2026-10-28 | Default shares and actions are set in the private deployment config, not in this repo | owner |
| 2026-10-29 | Ledger first: no request path; the proxy is off the roadmap (ADR 0003, 0010) | owner |
| 2026-10-29 | Name subgate (ADR 0001) | owner |
| 2026-10-29 | Own CI on GitHub-hosted runners (ADR 0011) | owner |

## Waiting on the owner

1. Language: .NET 10 Native AOT with the engine in switchknife, or Go (ADR 0002, 0004).
2. Approve the v0.1 spec (specs/v0.1.md), including the S1 spike that runs first.
3. Whether the plan's monthly API credits are linked to a Console organisation, and which Max tier: changes the `api` decision's economics (v0.2).

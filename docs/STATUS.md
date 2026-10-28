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
| 2026-10-29 | .NET 10 Native AOT; the engine is `Switchknife.Pace` in switchknife (ADR 0002, 0004) | owner |
| 2026-10-29 | v0.1 spec approved, spike S1 first | owner |

## Next

1. Spike S1 (`scripts/spike-s1.sh`). If it fails, stop and report before building.
2. v0.1 issues per the spec's work split, at most 3 open PRs at once.

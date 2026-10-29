# Status

Phase 0 (design) with v0.1 code under way: the provider parsers are in review (#9) and the engine `Switchknife.Pace` is merged in the switchknife repository (#36, #37; cycle 1, 2026-10-29), unreleased. Worker and session ids are kept in the owner's private notes, not here.

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
| 2026-10-29 | v0.2 weight-controller spec proposed: the plan is published, a gateway writer is an optional separate adapter (ADR 0013) | lead |
| 2026-10-29 | Build v0.1 as a ledger with `hold` and `GET /v1/plan`; no gateway writer (ADR 0013 amended, v0.2 spec cycle plan re-cut) | owner |
| 2026-10-29 | S1 passed: `/usage` gives the meters at $0; run events carry both windows in an undocumented `unifiedWindows` (ADR 0012) | lead |

## Next

Cycle plan: [v0.2 spec](specs/v0.2-weight-controller.md#cycle-plan).

1. Cycle 1, done 2026-10-29: `Switchknife.Pace` engine and attribution (switchknife#32, #33; merged as #36, #37). T1 to T6, T16, T19 and the attribution simulation T3 pass in its CI. Open: tag `pace/v0.1.0` (its publish to nuget.org waits for the owner's approval in the release environment).
2. Cycle 2: subgate on the engine: parsers (#9, #3), store, policy, observe-mode API (#4), CLI (#5). Needs `pace/v0.1.0` on nuget.org.
3. Release-please needs the `egoushka-release` App on this repository and its private key as a secret (owner).

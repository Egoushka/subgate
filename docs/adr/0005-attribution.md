# 0005. Attribution: apportion by reported use, calibrate when some use is unreported

- Status: proposed
- Date: 2026-10-28, updated 2026-10-29 for the ledger (ADR 0003)

## Context

The plan's weekly meter is account-wide, with a resolution of one point of the week (0.01 in the response headers, an integer percent in the status line). One `claude -p` run of 4 calls and about 280k tokens (256k cache reads) did not move it. Consumers report their own use per job (ADR 0012); some use is never reported: the owner's claude.ai and mobile apps, and any consumer that doesn't ask.

## Evidence (simulation, 2026-10-28)

A simulated week: three consumers with shares 25/10/10%, the account ending at 90%, the meter quantised to 0.01, reports out of order by up to 30 s, 40 seeds per cell. Error is the worst consumer's 95th percentile, in points of the week.

| Share of use that is never reported | Model-weight error | Delta per job | Apportion by tokens | Calibrated |
|---|---|---|---|---|
| none | 0 | 6.0 | **0.5** | 4.2 |
| none | ±30% | 5.0 | 3.7 | 4.3 |
| half | 0 | 15.9 | 12.1 | **3.1** |
| half | ±30% | 17.0 | 14.7 | **4.0** |
| all | ±30% | 32.0 | 27.3 | **4.5** |

- **Apportion**: split each meter rise across consumers by their weighted use since the last rise.
- **Calibrated**: share_c = weighted use_c × k, where k = meter rise ÷ reported weighted use, fitted only in quiet hours (configurable, default 00:00–08:00 local) when the owner is idle. Untracked use = meter − Σ consumers.

## Options

1. Delta per job (meter before and after) — rejected: one job rarely moves a 1-point step, and jobs overlap.
2. Apportion only — exact when every use is reported, 12–27 points off otherwise.
3. Calibrated only — 3–5 points everywhere, never exact.
4. **Both**: apportion while no untracked use is visible (Σ attributed ≈ meter within one step); otherwise calibrated, with k refitted weekly.

## Decision

Option 4. Weighted use is the per-model cost at list price that Claude Code reports with each run (ADR 0012). Whether Anthropic weighs plan usage that way is UNKNOWN; the ±30% rows show the cost of being wrong. The meter itself is exact, so the account-level pace line is always the backstop: a consumer is also `OVER_PACE` when the account is over the total pace line.

v0.1 exports both estimates as gauges. After a week of real use, a holdout test (v0.1 spec, D5) fits k on four nights and predicts the other three nights' meter rise from reported use; it decides whether v0.2 can enforce on the estimate.

## Consequences

Acceptance target, run as a test on the simulation: worst consumer ≤ 5 points at p95 in every cell above. Gating decisions near a share boundary carry this error; shares are set with that margin.

## Reopen if

Anthropic exposes per-request plan cost, or a finer-grained meter; or a week of real data shows the calibrated error above 5 points.

# 0005. Attribution: apportion by tokens, calibrate when use bypasses subgate

- Status: proposed
- Date: 2026-10-28

## Context

`7d-utilization` is account-wide, with a resolution of 0.01: one step is one point of the week. One `claude -p` run of 4 calls and about 280k tokens (256k cache reads) did not move it. Not every reply carries the headers (the first call of a run, probably token counting, had none). Some of the owner's use never passes subgate (the claude.ai apps, the mobile app).

## Evidence (simulation, 2026-10-28)

A simulated week: three consumers with shares 25/10/10%, the account ending at 90%, the header quantised to 0.01, replies out of order by up to 30 s, 40 seeds per cell. Error is the worst consumer's 95th percentile, in points of the week.

| Share of owner's use that bypasses subgate | Model-weight error | Delta per call | Apportion by tokens | Calibrated |
|---|---|---|---|---|
| none | 0 | 6.0 | **0.5** | 4.2 |
| none | ±30% | 5.0 | 3.7 | 4.3 |
| half | 0 | 15.9 | 12.1 | **3.1** |
| half | ±30% | 17.0 | 14.7 | **4.0** |
| all | ±30% | 32.0 | 27.3 | **4.5** |

- **Apportion**: split each header rise across consumers by their model-weighted tokens since the last rise.
- **Calibrated**: share_c = weighted tokens_c × k, where k = header rise ÷ tracked weighted tokens, fitted only in quiet hours (configurable, default 00:00–08:00 local) when the owner is idle. Untracked use = header − Σ consumers.

## Options

1. Delta per call — rejected: one call almost never moves a 1-point step.
2. Apportion only — exact when everything passes subgate, 12–27 points off otherwise.
3. Calibrated only — 3–5 points everywhere, never exact.
4. **Both**: apportion while no untracked use is visible (Σ attributed ≈ header within one step); otherwise calibrated, with k refitted weekly.

## Decision

Option 4. Model weights start at API price ratios (input, output, cache read, cache write per model), from config. Whether Anthropic weighs plan usage that way is UNKNOWN; the ±30% rows show the cost of being wrong. The header itself is exact, so the account-level pace line is always the backstop: a consumer is also OVER when the account is over the total pace line.

v0.1 exports both estimates as gauges; a week of real traffic, compared with the gateway's per-key token counts, decides whether the switch rule holds.

## Consequences

Acceptance target, run as a test on the simulation: worst consumer ≤ 5 points at p95 in every cell above. Gating decisions near a share boundary carry this error; shares are set with that margin.

## Reopen if

Anthropic exposes per-request plan cost, or a finer-grained header; or a week of real data shows the calibrated error above 5 points.

# 0008. Failure modes: the owner never depends on subgate; automation holds

- Status: proposed
- Date: 2026-10-29

## Context

Out of the request path (ADR 0003), subgate can't block a call, so it can't break one either. What's left is what each client does when subgate is down or wrong.

## Decision

| Failure | Owner | Automation (`subgate run`, or a client calling `/v1/admit`) |
|---|---|---|
| subgate down | unaffected: the owner never asks; `subgate statusline` times out after 300 ms and prints the plain status line | in enforce mode (v0.2) `admit` fails closed: the job doesn't start and `subgate run` exits 75 (`EX_TEMPFAIL`). In observe mode (v0.1), and with `--on-unavailable=run`, the job runs anyway and its report is queued in a local file and sent when subgate is back |
| subgate restarts mid-job | — | the lease is in SQLite and survives; the job reports at the end as usual |
| Report never arrives (crash, kill) | — | the lease expires and releases its reservation; the use shows up as untracked in the next meter rise (ADR 0005); the audit log names the consumer |
| Meter stale (no signal for 6 h, configurable) | — | state `STALE`; `admit` answers per the consumer's `on_stale` setting, default `hold` |
| No meter since start | — | `admit` allows; the first report or sample delivers one |
| A 429 seen by any consumer | — | the reporter posts `status: rejected`; state `LIMITED` for everyone until `resets_at`; `admit` answers `hold` with `retry_after_s` |
| Clock skew | — | the week is keyed by the meter's own `resets_at`, never by a local weekday; elapsed time is local clock against that reset; `subgate_signal_age_seconds` and the reporters' clock offset (each report carries its send time) are gauges, alerting past 60 s |
| Store unavailable at start | — | start with empty counters and say so in `/v1/status`; counters rebuild from the next reports |

## Consequences

The fail-open requirement for the owner is met by construction: nothing on the owner's path calls subgate synchronously except the status line, which has a timeout. A subgate outage pauses automation, which is the safe side.

## Reopen if

A consumer needs to run during outages by default.

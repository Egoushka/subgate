# 0012. Signal sources: what the clients already get, plus a `/usage` sampler

- Status: proposed
- Date: 2026-10-29

## Context

Out of the request path (ADR 0003), subgate needs two things from somewhere else: the plan's account-wide meter (utilization and reset per window) and each consumer's usage per job. Anthropic documents several ways a client receives both (read 2026-10-29).

## Evidence

| Source | What it carries | Where documented |
|---|---|---|
| `rate_limit_event` in Agent SDK and `claude -p --output-format stream-json --verbose` output | `status` (`allowed`, `allowed_warning`, `rejected`), `resetsAt`, `utilization` (0–1), and in Python `rate_limit_type` (`five_hour`, `seven_day`, …) | [TypeScript SDK](https://code.claude.com/docs/en/agent-sdk/typescript) `SDKRateLimitEvent`; [Python SDK](https://code.claude.com/docs/en/agent-sdk/python) `RateLimitInfo`. TypeScript describes it as "emitted when the session encounters a rate limit": how often it arrives under normal use is UNKNOWN |
| `/usage` sent as a prompt | `usage_report.rate_limits.limits[]`: `kind` (`session`, `weekly_all`, `weekly_scoped`), `percent` (0–100), `resets_at`; `null` when the token lacks the `user:profile` scope | TypeScript SDK `SDKUsageReport` (marked experimental). Whether `claude -p "/usage" --output-format stream-json` carries it the same way as the SDK is UNKNOWN |
| Interactive status line input | `rate_limits.five_hour` / `seven_day`: `used_percentage` (0–100), `resets_at`; `cost.total_cost_usd` for the session | [Status line](https://code.claude.com/docs/en/statusline), "Rate limit usage" |
| Result of a `-p` or SDK run | `modelUsage` per model, `total_cost_usd` (a client-side estimate at list price) | [Headless](https://code.claude.com/docs/en/headless), [cost tracking](https://code.claude.com/docs/en/agent-sdk/cost-tracking) |
| A 429 | `error: rate_limit` in the result and the `StopFailure` hook | [Hooks](https://code.claude.com/docs/en/hooks) |

## Decision

Three reporters, all posting to `POST /v1/signal` or `POST /v1/usage` (ADR 0007):

1. **`subgate run`** (the client CLI). Wraps a job: asks `POST /v1/admit`, runs the command, reads its stream-json output for `rate_limit_event`s and the final result's `modelUsage`, and reports both.
2. **`subgate sample`**. On a timer (default every 30 min), runs the unmodified Claude Code on the owner's login with the `/usage` command and posts the meter. It costs no model tokens if `/usage` is a local command, as the SDK docs describe (UNKNOWN until measured: check S1 in the v0.1 spec). This is what keeps the meter fresh at night, when only automation runs and the quiet-hours fit of k needs it.
3. **`subgate statusline`**. A status-line command for the owner's interactive sessions: posts `rate_limits` and the session's running cost (as the `owner` consumer, ADR 0006), then prints a status line with the owner's remaining week. A 300 ms timeout; it prints even when subgate is down.

**Window mapping.** The TypeScript `rate_limit_event` has no window type: a reading whose `resetsAt` is at most 5 hours away is the 5-hour window, otherwise the 7-day one. The Python shape's `rate_limit_type` wins when present.

**Weights.** A consumer's usage is its run's per-model cost at list price (`modelUsage` costs), which is the API-price weighting ADR 0005 assumed, computed by Claude Code itself.

## Consequences

subgate reads and stores no credential: each reporter runs where the client runs and posts numbers only. The sampler adds one small Claude Code run every 30 minutes on the owner's login.

## Reopen if

The `/usage` report or `rate_limit_event` changes shape (both are marked or behave as experimental), or the sampler turns out to cost model tokens.

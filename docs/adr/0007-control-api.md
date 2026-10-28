# 0007. The API: admit, report, signal; consoles sit on top

- Status: proposed
- Date: 2026-10-29

## Context

The ledger is its API (ADR 0003). Limits change mid-week, and approvals must reach the owner wherever they are: an orchestrator's console, a chat assistant, a phone notification. Building one front end into subgate would pick one of them.

## Decision

| Method and path | Does | Auth | Lands in |
|---|---|---|---|
| `POST /v1/admit` `{consumer, model?, estimate_usd?}` | Answers `{decision, would, lease_id, lease_ttl_s, share, used, allowance, remaining, state, reset_at, retry_after_s?, model?, approval_id?}`. `decision` is `allow`, `hold`, `api` (use your own API credential) or `cheap` (use this `model` on your API credential). In observe mode (v0.1) it is always `allow`, and `would` says what enforce mode would answer. | consumer | v0.1 (observe), v0.2 (enforce) |
| `POST /v1/usage` `{lease_id?, session_id?, consumer, route: "plan" \| "api", models?: {name: {input, output, cache_read, cache_write, cost_usd}}, cost_usd?, cumulative?: bool}` | Records a job's use and closes its lease. A status-line report sends a session's running total (`session_id`, `cost_usd`, `cumulative: true`); subgate keeps the highest total per session and counts the increase, so repeats are harmless. `route: api` counts toward the daily dollar cap, not the plan share. | consumer | v0.1 |
| `POST /v1/signal` `{source, sent_at, windows: [{kind: "5h" \| "7d" \| "7d_scoped", scope?, utilization, resets_at, status?}]}` | Records the plan's meter. `utilization` is 0–1 (reporters divide percentages by 100); `resets_at` is Unix seconds. | signal, or a consumer token | v0.1 |
| `GET /v1/status` | account windows, per consumer: share, used, allowance, state, open leases, estimator | open on the listen address | v0.1 |
| `GET /metrics`, `GET /healthz` | Prometheus; liveness | open | v0.1 |
| `GET /v1/consumers`, `PUT /v1/consumers/{name}`, `DELETE /v1/consumers/{name}/override` | read, override, clear: share, action, cheap model, daily dollar cap. The status shows each override as drift from the file. | admin | v0.2 |
| `GET /v1/audit?since=` | append-only log of decisions, state changes and edits | admin | v0.2 |
| `GET /v1/approvals?state=pending`, `POST /v1/approvals/{id}` | approve `{"extra_share": 0.05}` or `{"jobs": N}`, or deny; a grant expires at the week's reset | admin | v0.3 |
| webhook `on_approval_request` | POST to a configured URL | — | v0.3 |

**Leases.** An `allow` reserves the job's estimate (given, or the consumer's median job cost) until `POST /v1/usage` closes it or `lease_ttl_s` passes (default 2 h). Reservations count toward `used`, so ten jobs admitted at once can't all fit the same remaining share. An expired lease releases its reservation and is logged as unreported.

Writes need a bearer token (ADR 0006), compared in fixed time. Admin endpoints are off when no admin token is configured. The listen address defaults to loopback.

## Consequences

An orchestrator's admission check is one `POST /v1/admit`; its console edits limits with `PUT /v1/consumers/{name}`. Any other front end is the same few calls.

## Reopen if

A front end needs push updates (add server-sent events on `/v1/events`).

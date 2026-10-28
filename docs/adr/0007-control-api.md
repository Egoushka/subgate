# 0007. Control API first; consoles sit on top

- Status: proposed
- Date: 2026-10-28

## Context

Limits change mid-week, and approvals must reach the owner wherever they are: an orchestrator's console, a chat assistant, a phone notification. Building one front end into subgate would pick one of them.

## Decision

subgate exposes a small JSON API and nothing else; front ends are clients.

| Method and path | Does | Lands in |
|---|---|---|
| `GET /v1/status` | account windows (utilization, reset, state), per consumer: share, used, allowance, state, action, estimator | v0.1 |
| `GET /metrics` | Prometheus | v0.1 |
| `GET /healthz` | liveness; `200` while the proxy accepts connections | v0.1 |
| `GET /v1/consumers`, `PUT /v1/consumers/{name}`, `DELETE /v1/consumers/{name}/override` | read, override, clear: share, action, cheap model, daily dollar cap. The status shows each override as drift from the file. | v0.2 |
| `GET /v1/audit?since=` | append-only log of state changes, actions and edits | v0.2 |
| `GET /v1/approvals?state=pending`, `POST /v1/approvals/{id}` | approve `{"extra_share": 0.05}` or `{"requests": N}`, or deny; a grant expires at the week's reset | v0.3 |
| `POST /v1/admit` | ledger mode (ADR 0010): may consumer X start a job now? | v0.3 |
| webhook `on_approval_request` | POST to a configured URL (a push service, an assistant, an orchestrator) | v0.3 |

Writes need a bearer token (`SUBGATE_ADMIN_TOKEN`), compared in fixed time; with no token configured, write endpoints are off. Reads are open on the listen address, which defaults to loopback.

A held reply carries `x-subgate-approval: <id>` when the consumer's policy allows approvals, so a client can show the request without polling.

## Consequences

The author's orchestrator console edits limits through `PUT /v1/consumers/{name}`; its admission check reads `GET /v1/status`. Any other UI is the same few calls.

## Reopen if

A front end needs push updates (add server-sent events on `/v1/events`).

# 0008. Failure modes: the owner fails open, automation fails to hold

- Status: proposed
- Date: 2026-10-28

## Context

subgate sits in the request path of the owner's own interactive sessions. Those must never be blocked by subgate, even when it is down. Automation, on the other hand, should stop rather than spend blind.

## Decision

| Failure | Owner | Automation |
|---|---|---|
| A bug in the tap or the engine (panic, parse error, store error) | forwarded; the request never waits on metering | forwarded; metering error counted in `subgate_tap_errors_total` |
| subgate process down | **fails over** to the upstream directly (below) | connection refused: the client errors, which is a hold |
| Upstream down or 5xx | passed through unchanged | passed through unchanged |
| Headers missing on a reply | counted by tokens only | counted by tokens only |
| No snapshot since start | forward; the first reply delivers one | forward |
| Snapshot older than 6 h (configurable) | forward | state `STALE`; action per `on_stale`, default `hold` |
| Clock skew | the week is keyed by the header's `7d-reset`, never by a local weekday; elapsed time uses the local clock against that reset, and a skew gauge (`subgate_clock_skew_seconds`, from the upstream `date` header) alerts past 60 s | same |
| Store unavailable at start | start with empty counters and say so in `/v1/status` | same; counters rebuild from the next header rise |

**Metering never blocks bytes.** The proxy copies response chunks into a bounded channel; when it's full, the tap drops the chunk and counts it (`subgate_tap_dropped_total`). Streaming latency doesn't depend on the engine.

**Fail-over when subgate is down**, three layers, each optional:

1. **In front (recommended for a server deployment):** a reverse proxy with a health-checked primary (subgate) and a backup (the gateway or `api.anthropic.com`) for the owner's route only. Survives an outage mid-session.
2. **At client start:** `subgate env` prints `ANTHROPIC_BASE_URL=<subgate>` when `/healthz` answers within 500 ms, otherwise the direct URL. A shell alias runs it before `claude`. Covers a laptop with no edge proxy; doesn't survive an outage mid-session.
3. **In code:** layer "bug in the tap" above.

v0.1 ships 2 and 3; 1 is deployment config, and the v0.1 deploy check tests it on the author's server (spec, check D3).

## Consequences

A crash of subgate stops automation until it restarts. The container runs with `restart: unless-stopped`, and an alert fires on `up == 0` for 2 minutes.

## Reopen if

Claude Code gains a native fallback base URL.

# 0010. Two transports over one engine: proxy now, ledger as the fallback

- Status: proposed
- Date: 2026-10-28

## Context

The Usage Policy effective 2026-11-12 forbids "services that route requests through consumer subscriptions" (legal.md, claim 3). Whether a single-owner, self-hosted proxy is such a service is UNKNOWN. If it is, the proxy transport must go, and the pacing should survive that.

## Options

1. **Proxy** (ADR 0003): in the request path; reads the plan's headers on every reply; can act on a request (hold, swap to an API key, cheaper model). Accurate, works with unmodified clients.
2. **Ledger**: not in the request path. Clients call `POST /v1/admit {consumer, estimate}` before a job and get `allow`, `hold` (with an approval id) or `api`/`cheap` (the client switches credential or model itself). Usage arrives after the fact from the clients' own telemetry (Claude Code exports token usage over OpenTelemetry) or from a gateway's per-key counts. The account signal needs a source outside the request path: UNKNOWN which one is permitted (the status line's data reaches only the interactive client).

## Decision

Build the proxy first, keep the engine transport-neutral (ADR 0004), and add the ledger in v0.3. If the stop condition in legal.md triggers, the proxy transport is removed and the ledger stays.

## Consequences

`engine` takes usage events from any source, not only from a response tap. The ledger's admission check works per job, not per request, so it's coarser.

## Reopen if

Anthropic clarifies the Usage Policy line either way.

# 0003. Where it sits: a keyless proxy in front of the gateway

- Status: proposed
- Date: 2026-10-28

## Context

Two places can read the headers and act: a plugin inside the LLM gateway (Bifrost), or a separate proxy in front of it. Bifrost holds every provider key of the deployment.

## Evidence (spike, 2026-10-28)

- Stock Bifrost v2.3.2 forwards `anthropic-ratelimit-unified-*` on streamed replies and on 429s; a proxy in front sees everything it needs.
- Bifrost v2.3.2 cannot fail an OAuth pass-through call over to an API key: its fallback path keeps the caller's pass-through headers, so a same-provider fallback re-sends the OAuth token (tested with a mock upstream). A credential swap has to happen before Bifrost, in the client or in a proxy in front.
- Bifrost's logs store upstream 429s but no response headers.

## Options

1. **Bifrost plugin.** One process fewer. Blast radius: a bug or a panic in new code runs inside the process that holds every provider key and serves every consumer, including ones subgate doesn't meter. Tied to Bifrost's plugin API and release cadence. Can't swap credentials on pass-through (above) without patching Bifrost.
2. **Standalone proxy in front, holding no provider keys.** Blast radius: only the consumers routed through it; a crash stops those consumers (automation holds, which is the safe side; the owner's route fails over, ADR 0008). Holds at most the gateway keys the `api` action swaps in, never a provider key. Upstream is configurable, so it works with any gateway or straight to `api.anthropic.com`.
3. **Client-side only (each consumer reads a shared counter).** No proxy, but every consumer needs code, and no one sees the headers (legal.md claim 9: they reach the client binary, not its caller). Becomes ADR 0010's ledger mode, which still needs a signal source.

## Decision

Option 2. subgate is a separate process: `client → subgate → gateway (optional) → Anthropic`. A `hold`-only deployment holds no secrets at all.

## Consequences

One more hop: +1.5 ms p50 and +3 ms p95 to first byte in the spike (ADR 0002). Without a gateway, the `api` action needs a real provider key in subgate; the docs say so and recommend a gateway, or `hold` as the only action, for that case.

## Reopen if

Bifrost (or another gateway) exposes response headers to plugins and supports credential swap on pass-through, and the extra hop measurably matters.

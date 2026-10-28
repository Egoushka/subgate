# 0003. Where it sits: out of the request path

- Status: accepted (owner, 2026-10-29)
- Date: 2026-10-29

## Context

Three places can pace a subscription: a plugin inside an LLM gateway, a proxy in front of it, or a ledger beside it that clients ask before they start. Anthropic's Usage Policy, effective 2026-11-12, forbids "services that route requests through consumer subscriptions" (legal.md, claim 3).

## Evidence

- A proxy and a gateway plugin both route requests through the subscription. Whether a single-owner proxy is such a "service" is UNKNOWN; the owner chose not to depend on the narrow reading.
- Everything a ledger needs is documented by Anthropic without a proxy (ADR 0012): the plan's meter reaches the Agent SDK and `claude -p` as `rate_limit_event`, the `/usage` command returns it as a structured `usage_report`, and the interactive status line receives it as `rate_limits`; each run's result carries `modelUsage` and `total_cost_usd`.
- A spike (2026-10-28) found that a gateway (Bifrost v2.3.2) can't swap an OAuth pass-through call to an API key on its own; in a ledger the client switches credential itself, so that limit no longer matters.

## Options

1. **Gateway plugin.** Routes requests; runs inside the process that holds every provider key. Rejected.
2. **Proxy in front of the gateway.** Routes requests; the earlier design (git history, 2026-10-28). Rejected on the terms.
3. **Ledger beside the clients.** Consumers ask "may I start, and how much is left?", call Anthropic directly on their own login with the unmodified client, and report what they used. No request passes through subgate, and subgate never sees a credential.

## Decision

Option 3. subgate is a small HTTP service plus a client CLI. It holds no provider key, no OAuth token and no gateway key, in every mode.

## Consequences

- Pacing is cooperative: a consumer that doesn't ask is not paced, only seen (its use shows up as untracked in the account meter, ADR 0005). The client CLI (`subgate run`) makes asking a one-word change for any script.
- Decisions are per job, not per request: a long job admitted under its share can overrun it. A reservation at admission (ADR 0007) bounds that.
- subgate works with or without a gateway and never assumes one sits in front of the subscription.

## Reopen if

Anthropic states that a single-owner, self-hosted proxy is permitted, and per-request control is worth the extra moving part.

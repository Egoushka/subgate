# 0006. Consumer identity: a hashed header, no secrets in config

- Status: proposed
- Date: 2026-10-28

## Context

Every consumer sends the same OAuth token (one owner, one login), so the token can't say who is calling. Behind a gateway, each consumer already sends its own gateway key (Bifrost: `x-bf-vk`). Without a gateway there is no per-consumer header unless the client adds one; Claude Code can, with `ANTHROPIC_CUSTOM_HEADERS`.

## Decision

- The identity header is configurable: `x-bf-vk` behind Bifrost, `x-subgate-consumer` by default.
- The config lists consumers by `sha256(header value)`, so the config file and the public repo hold no secret. `subgate hash <value>` prints the hash.
- An unknown or missing identity maps to the `owner` consumer: measured, never gated (ADR 0008). Reason: the safe failure for the owner is pass-through, and automation is configured on purpose.
- The header is forwarded unchanged (the gateway needs it) and never logged.

## Consequences

A consumer that forgets its header is counted as the owner: it can't be gated, and the owner's share looks larger. `subgate_requests_total{consumer="owner"}` with a `user_agent` label split lets the owner spot it.

## Reopen if

A deployment needs unknown callers rejected rather than passed (add `unknown: reject`).

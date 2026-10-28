# 0006. Consumer identity: a bearer token per consumer, stored as a hash

- Status: proposed
- Date: 2026-10-29

## Context

Every consumer runs on the same login, so the login can't say who is calling. The ledger (ADR 0003) sees only its own API calls: admit, usage, signal.

## Decision

- Each consumer gets a random bearer token for subgate's API. The config lists consumers by name with `sha256(token)`, so the config file and the public repo hold no secret. `subgate token new <name>` prints a token and its hash.
- `subgate sample` uses a `signal` token, which may post signals and nothing else.
- `subgate statusline` uses the `owner` consumer's token: it posts signals and the owner's session use.
- The owner is a consumer named `owner`: its reports are measured, and `admit` always answers `allow` for it.
- Tokens are compared in fixed time and never logged.

## Consequences

A consumer without a token can't report, so its use counts as untracked (ADR 0005). Tokens are subgate's own and give no access to Anthropic.

## Reopen if

A deployment needs identities from an existing system (mTLS, an SSO header).

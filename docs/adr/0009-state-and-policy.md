# 0009. State and policy: memory first, SQLite next; defaults in a file, overrides in the store

- Status: proposed
- Date: 2026-10-28

## Context

Counters must survive a restart mid-week, or the pace line forgets what was spent. One instance serves one owner; there is no shared state across hosts.

## Decision

- **State.** `store/memory` for tests and v0.1's first cut; `store/sqlite` (pure-Go `modernc.org/sqlite`, so the binary stays static with no cgo) before the v0.1 deploy. Tables: counters per (consumer, window, `reset`), the last snapshot per window, k with its fit time, overrides, approvals, audit. Counters are keyed by Anthropic's `7d-reset`, so a new week starts a new row and old rows are kept 8 weeks for calibration.
- **Policy.** A YAML file (in the deployment's git) holds the defaults: consumers by identity hash, share, action, cheap model, daily dollar cap, quiet hours, model weights. Runtime overrides from the API live in the store and win until cleared. On start, the file is validated; shares summing past 100% are an error.
- Postgres only if two instances must share counters, which a single-owner tool doesn't need.

## Consequences

Back up one file. A restart loses nothing but in-flight taps.

## Reopen if

A deployment runs more than one instance.

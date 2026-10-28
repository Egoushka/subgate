# 0009. State and policy: memory first, SQLite next; defaults in a file, overrides in the store

- Status: proposed
- Date: 2026-10-28

## Context

Counters must survive a restart mid-week, or the pace line forgets what was spent. One instance serves one owner; there is no shared state across hosts.

## Decision

- **State.** An in-memory store for tests; SQLite before the v0.1 deploy (the driver follows ADR 0002). Tables: counters per (consumer, window, `resets_at`), the last meter reading per window, leases, k with its fit time, overrides, approvals, audit. Counters are keyed by the meter's weekly `resets_at`, so a new week starts a new row and old rows are kept 8 weeks for calibration.
- **Policy.** A YAML file (in the deployment's git) holds the defaults: consumers by token hash, share, action, cheap model, daily dollar cap, quiet hours, model weights. Runtime overrides from the API live in the store and win until cleared. On start, the file is validated; shares summing past 100% are an error.
- Postgres only if two instances must share counters, which a single-owner tool doesn't need.

## Consequences

Back up one file. A restart loses nothing; open leases survive it.

## Reopen if

A deployment runs more than one instance.

# 0013. The plan is published; a gateway writer is a separate adapter

- Status: proposed
- Date: 2026-10-29

## Context

The v0.2 idea was a controller that reads subgate's pace and writes a gateway's routing weights, so "X% of this consumer's calls go to the API key" follows the pace line (spec v0.2-weight-controller). Reading the gateway's source (Bifrost, tag `transports/v2.3.2`) found: weights can be changed at runtime through the admin API; that API has one credential for everything, budgets and keys included; and a weight cannot move a call from an OAuth pass-through to an API key, because the caller's token makes the gateway skip key selection and a fallback keeps the flag. Subscription calls also bypass the gateway by design (ADR 0003). legal.md says subgate holds no gateway key.

## Options

1. **The controller inside subgate writes the gateway.** Simplest. subgate would hold the gateway admin credential, which can edit virtual keys, budgets and providers and so lift every cap; breaks "no gateway key"; the credential sits in a service that parses output from other programs.
2. **subgate publishes the plan; clients apply it; an optional adapter writes the gateway.** The plan is numbers and a validity time, no credential. `subgate run` and scripts apply it per job, which also keeps a job on one model. The adapter is a separate process the owner runs, with a confined credential and an allow-list in front. subgate stays keyless.
3. **A pull request per weight change to the deployment repository.** Reviewable, no live credential. A merge restarts the gateway in the author's deployment, so it cannot follow a 5-minute tick.
4. **No weights, per-job decisions only.** The least moving parts. It leaves callers that cannot ask subgate (failover chains inside third-party services) with static weights.

## Decision

Option 2. The allocator lives in the engine (ADR 0004) and produces the plan; the adapter is optional and ships only if spikes W-S1 to W-S3 pass. Option 4 is the fallback if they don't.

## Consequences

- The primary split between subscription and API is made at the client, per job, from the plan. Gateway weights apply only to traffic already on API credentials, for consumers marked `weights_ok`.
- The plan expires (`valid_until`), which a gateway weight cannot; a dead controller is detected and clients fall to `on_stale`.
- The adapter is the only component that needs a gateway credential. The owner decides whether it counts as "outside subgate" under legal.md or whether the wording changes.

## Reopen if

A gateway offers scoped admin tokens (the adapter could then live in the service), or a gateway can switch credentials by weight without breaking the OAuth rule.

# CLAUDE.md — subgate

A ledger that paces each consumer's weekly share of one AI subscription: consumers ask before a job and report after; subgate is never in the request path. Public repository. Phase 0: docs only; code starts on the owner's OK of `docs/specs/v0.1.md`.

## Public vs private — the rule for every file

Never commit: hostnames, IP or tailnet addresses, gateway key names, token hashes from a real config, usage numbers or spend from a real account, session or message ids, absolute home paths, tokens. Real configs live in the deployment's private repo; `examples/` uses placeholders. Fixtures recorded from real runs are scrubbed of ids, prompts and numbers. Before a push: `gitleaks git --redact` and a grep against the gitignored `.private-terms` list.

## Rules

- Decisions: `docs/adr/NNNN-title.md` from `docs/adr/template.md`, with evidence or UNKNOWN.
- The engine does no I/O and takes the clock as a parameter (ADR 0004).
- subgate never accepts, stores or logs a provider credential, and never proxies a request (ADR 0003, docs/legal.md).
- Every acceptance check in a spec names a command and an expected result, and can fail.
- Commits: Conventional Commits; squash-merge; release-please makes every feature a patch.

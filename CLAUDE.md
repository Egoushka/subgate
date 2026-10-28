# CLAUDE.md — subgate

Go streaming proxy that paces each consumer's weekly share of one AI subscription. Public repository. Phase 0: docs only; code starts on the owner's OK of `docs/specs/v0.1.md`.

## Public vs private — the rule for every file

Never commit: hostnames, IP or tailnet addresses, gateway key names, identity hashes from a real config, usage numbers or spend from a real account, session or message ids, absolute home paths, tokens. Real configs live in the deployment's private repo; `examples/` uses placeholders. Before a push: `gitleaks git --redact` and a grep against the gitignored `.private-terms` list.

## Rules

- Decisions: `docs/adr/NNNN-title.md` from `docs/adr/template.md`, with evidence or UNKNOWN.
- The engine (`engine/`) does no I/O and takes the clock as a parameter (ADR 0004).
- Never log or store the `authorization` header, never rewrite client identity headers (docs/legal.md).
- Every acceptance check in a spec is a command that can fail.
- Commits: Conventional Commits; squash-merge; release-please makes every feature a patch.

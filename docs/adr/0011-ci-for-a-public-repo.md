# 0011. CI for a public repository

- Status: accepted (owner, 2026-10-29)
- Date: 2026-10-28

## Context

The author's shared CI lives in a private repository, built for .NET, with workflows that default to a self-hosted runner. GitHub lets other private repositories call workflows from a private repository; a public repository can't (UNKNOWN until tried: the first CI run settles it). A public repository must also never run fork pull requests on a self-hosted runner.

## Options

1. **Make the shared repository public.** One source of truth. Needs a review of everything in it for private details, and its self-hosted defaults stay unsafe for public callers.
2. **Publish a public subset** (build, lint and security workflows) and call it from here. Reusable by other public repos.
3. **Own CI here, on GitHub-hosted runners**: build and test on Linux and macOS, the language's own linters and vulnerability audit, `gitleaks` (binary pinned by SHA-256), `osv-scanner`, `actionlint`.

## Decision

Option 3 for v0.1, with the same pinning rules as the shared repository (actions pinned to a commit digest, tool binaries pinned to SHA-256, Renovate with a 7-day delay). Move to option 2 when a second public repository needs the same jobs.

## Consequences

The pre-commit config is the shared template; its .NET hook stays if ADR 0002 picks .NET. Renovate runs from the shared preset if the public repo may read it; otherwise its config is inline.

## Reopen if

A second public repository needs the same jobs.

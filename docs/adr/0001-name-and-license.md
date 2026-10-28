# 0001. Name and licence

- Status: proposed
- Date: 2026-10-28

## Context

The project needs a short name that says what it does (a gate on a subscription's share), is free on GitHub, Go and Homebrew, and keeps "Claude" and "Anthropic" out (trademark guidelines, legal.md claim 13).

## Evidence (checked 2026-10-28)

All candidates are free as `Egoushka/<name>` on GitHub (`gh repo view` fails) and as Homebrew formula and cask (API returns 404). A Go module path `github.com/Egoushka/<name>` is free when the repo is.

| Name | Same-name projects | Other conflicts | Verdict |
|---|---|---|---|
| **subgate** | three tiny unrelated repos (0–2 stars) | none found | keeps the working name; "sub" reads as subscription |
| quotaline | three repos in this exact niche ("AI coding plan limits, pace") | — | avoid |
| paceline | two repos, one a Claude-limit status line (24 and 3 stars) | a 2021 trademark filing for a rewards service | avoid |
| rationer | none | none found | cleanest, but reads as rationing food |
| stipend | two small repos (25, 39 stars) | none found | fits "paced allowance" |
| allot / allotment | a Go module `sbstjn/allot`; a React library with 1.2k stars | — | avoid |
| sharegate | — | ShareGate, a Microsoft 365 migration product | avoid |

No USPTO or EUIPO search was made.

## Decision

Name: **subgate**. Repository `Egoushka/subgate`, Go module `github.com/Egoushka/subgate`, binary `subgate`. Licence: MIT, like the author's other public libraries.

## Consequences

Container image `ghcr.io/egoushka/subgate`. A rename later costs a module-path change for every importer of the engine, so the name is settled before v0.1.

## Reopen if

A trademark search turns up a live mark on "subgate" in software, or the project's scope outgrows subscriptions.

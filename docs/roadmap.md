# Roadmap

Milestones are named goals with due dates. Each feature or fix is a patch release; closing a milestone cuts a minor version.

| Milestone | Due | Goal | Done when | Minor |
|---|---|---|---|---|
| **See the week** | 2026-11-13 | Observe only: automation asks and reports, the sampler and the status line keep the meter fresh, every consumer's share is attributed; nothing is refused. Deployed and watched for a week. | the v0.1 spec's checks pass, D5 included | 0.1.0 |
| **Pace the automation** | 2026-11-27 | Enforce mode: `admit` answers `hold`, `api` (with a daily dollar cap from reported cost, plan API credits first) or `cheap`; leases count in decisions; runtime limits through `PUT /v1/consumers`; audit log. | each decision proven by a test and once on real use; the owner never refused | 0.2.0 |
| **Ask before overrun** | 2026-12-11 | Approval requests with a webhook; a console on top of the API. | an approval raised by a held job is granted from a phone and the next `admit` allows | 0.3.0 |
| **A second plan** | 2027-01-08 | OpenAI or Codex subscription through the same engine. | a provider adapter with its own spike and tests | 0.4.0 |
| **Stable contracts** | when earned | `/v1` API and config schema frozen. | a second user runs it | 1.0.0 |

Not on the roadmap: a proxy transport (ADR 0010). It comes back only if Anthropic clearly permits a single-owner, self-hosted proxy.

## Dates that move this

- 2026-11-12: Anthropic's new Usage Policy takes effect (legal.md, claim 3). Re-read it that day and check the stop condition.
- S1 (spec): if neither the `/usage` report nor `rate_limit_event` gives a usable meter headlessly, v0.1 is re-planned before code.
- Each milestone's spec is approved before its code starts (weekly cycle: plan Sunday, release Friday).

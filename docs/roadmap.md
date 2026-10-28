# Roadmap

Milestones are named goals with due dates. Each feature or fix is a patch release; closing a milestone cuts a minor version.

| Milestone | Due | Goal | Done when | Minor |
|---|---|---|---|---|
| **See the week** | 2026-11-15 | Signal only: every subscription call is measured and attributed; nothing is gated. Deployed and watched for a week. | the v0.1 spec's checks pass, D4 included | 0.1.0 |
| **Pace the automation** | 2026-11-29 | Enforce mode: `hold`, `api` with a daily dollar cap (plan API credits first), `cheap`; runtime limits through `PUT /v1/consumers`; audit log. | each action proven by a test and once on real traffic; no owner call ever gated | 0.2.0 |
| **Ask before overrun** | 2026-12-13 | Approval requests with a webhook; a console on top of the API; the ledger transport (`POST /v1/admit`). | an approval raised by a held consumer is granted from a phone and the next call passes | 0.3.0 |
| **A second plan** | 2027-01-10 | OpenAI or Codex subscription through the same engine. | a provider adapter with its own header spike and tests | 0.4.0 |
| **Stable contracts** | when earned | `/v1` API and config schema frozen; engine split into its own module if a second program imports it. | a second user runs it | 1.0.0 |

## Dates that move this

- 2026-11-12: Anthropic's new Usage Policy takes effect (legal.md, claim 3), during the first watch week. Re-read it that day; if the stop condition triggers, the proxy goes and the ledger moves forward.
- Each milestone's spec is approved before its code starts (weekly cycle: plan Sunday, release Friday).

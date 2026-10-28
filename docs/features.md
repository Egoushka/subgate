# Features, and why each one exists

The problem: one Claude Max subscription serves its owner's interactive work and several automated programs (a coding-agent orchestrator, a memory service, an assistant, a review guard). The plan has one weekly allowance for all of them. Nothing stops one program from spending the week by Wednesday, and nothing tells any program how much of the week is left. subgate gives each program a share, paces it, and tells each program what to do when it's over. It does this beside the subscription, never in its request path (ADR 0003).

**Evidence policy.** The numbers behind these features come from the author's own setup and stay private (a "private measurement" below). What is public is Anthropic's documentation, the measured behaviour of the plan's meter, and the simulation (see [architecture.md](architecture.md)).

Versions: v0.1 is signal only (observe), v0.2 enforcement and actions, v0.3 approvals and the console ([roadmap.md](roadmap.md)).

| # | Feature | Problem it solves | Who it serves | Evidence | Lands in |
|---|---|---|---|---|---|
| 1 | **Collect the plan's own meter** from what clients already receive: `rate_limit_event` in run output, a `/usage` sampler, the owner's status line | The owner sees the weekly number only in the interactive status line; no program sees it. | every consumer, the owner | Documented by Anthropic (ADR 0012). | v0.1 |
| 2 | **`subgate run`**: wrap any job to ask first and report after | Pacing must cost a script one word, not code. | any automation | — | v0.1 |
| 3 | **Per-consumer weekly share** | One program can burn the week. | the owner | Private measurement: automation's subscription use, priced at API rates, is several times the owner's paid API spend over the same week. | v0.1 (measured), v0.2 (enforced) |
| 4 | **Attribution under a coarse meter** | The meter is account-wide and moves in steps of one point of the week; before-and-after readings can't say who spent what. | the owner, any consumer | Simulated week: calibrated estimate 3.1–4.5 points p95 at any level of unreported use; apportioning 0.5 when everything is reported (ADR 0005). | v0.1 |
| 5 | **The pace line** | A fixed weekly cap lets a program spend it all on day one. allowance(t) = share × elapsed ÷ 7 days; unused allowance carries forward. | the owner | The week resets at the meter's own `resets_at`, not a fixed weekday. | v0.1 (gauge), v0.2 (enforced) |
| 6 | **States OK / OVER_PACE / LIMITED / STALE** | Consumers need one answer, not a calculation. | consumers, dashboards | A 429 reaches callers as `error: rate_limit` with a reset time. | v0.1 |
| 7 | **Leases** | Ten jobs admitted at once can't all fit the same remaining share. An admitted job reserves its estimate until it reports. | parallel automation | — | v0.1 (recorded), v0.2 (counted in decisions) |
| 8 | **Decision `hold`** | A program over its share should not start, and should know when to try again. `retry_after_s` and, from v0.3, an approval id; `subgate run` exits 75. | automation with retry logic | The author's orchestrator already holds starts on a subscription 429. | v0.2 |
| 9 | **Decision `api`, with a daily dollar cap** | Some work must not wait. The client runs on its own API credential (plan API credits first), under a daily cap from its reported cost, past which the answer becomes `hold`. subgate holds no key. | time-sensitive automation | Max plans include $100–200 a month of API credits that expire unused (legal.md, claim 12). | v0.2 |
| 10 | **Decision `cheap`** | Work that tolerates a smaller model shouldn't stop. Like `api`, with a model to use. | background jobs | Private measurement: one background job already moved to a small model on its own. | v0.2 |
| 11 | **The owner is measured, never gated** | The owner's own sessions must never wait on their tool. They never call `admit`; the status line reporter has a 300 ms timeout. | the owner | Reporting the owner's use is what makes attribution accurate (ADR 0005 table). | v0.1 |
| 12 | **Approval requests** | Going past the line should be the owner's decision, made in a moment: "+N% this week" or "N more jobs". A grant expires at the week's reset; a webhook reaches a phone, an assistant or a console. | the owner | — | v0.3 |
| 13 | **Runtime-editable limits, defaults in git** | Shares change mid-week; a redeploy to change a number is too slow. `PUT /v1/consumers/{name}` overrides the file until cleared; the status shows the drift. | the owner, consoles | The orchestrator's console already edits server-kept options this way. | v0.2 (API), v0.3 (console) |
| 14 | **Prometheus gauges and `/v1/status`** | Dashboards and other programs' admission logic need the numbers. | Grafana, orchestrators | — | v0.1 |
| 15 | **Audit log** | "Why was this held?" and "who raised the share?" need an answer after the fact. | the owner | — | v0.2 |
| 16 | **A second provider** | OpenAI and Codex plans have the same weekly-pool problem. Same engine, a new provider adapter. | other users | UNKNOWN where those plans expose their meter; checked in that milestone. | after v0.3 |

## Out of scope, and why

| Not doing | Why |
|---|---|
| A proxy or any transport in the request path | The Usage Policy line effective 2026-11-12 (legal.md, claim 3); the owner chose the ledger (ADR 0003, 0010). Reopened only if the terms clearly allow it. |
| Hosting for other people, multiple owners, account pooling or rotation | Forbidden by the terms (legal.md). |
| Holding any credential: OAuth tokens, API keys, gateway keys | Not needed: clients switch credential themselves. Holding one is the "intermediate credentials" line. |
| Forcing a consumer that doesn't ask | Out of the request path, subgate can only see such use (as unreported) and say so. |
| Dollar budgets for subscription use | It isn't billed per token. Dollar caps apply only to the `api` and `cheap` routes. |
| Prompt or response logging | Only per-model usage and cost are recorded. |

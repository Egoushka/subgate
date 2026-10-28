# Features, and why each one exists

The problem: one Claude Max subscription serves its owner's interactive work and several automated programs (a coding-agent orchestrator, a memory service, an assistant, a review guard). The plan has one weekly allowance for all of them. Nothing stops one program from spending the week by Wednesday, and nothing tells any program how much of the week is left. subgate gives each program a share, paces it, and decides what happens on overrun.

**Evidence policy.** The numbers behind these features come from the author's own setup and stay private (a "private measurement" below). What is public is the measured behaviour of Anthropic's headers and of the code (see [architecture.md](architecture.md)).

Versions: v0.1 is signal only, v0.2 gating, v0.3 approvals and console ([roadmap.md](roadmap.md)).

| # | Feature | Problem it solves | Who it serves | Evidence | Lands in |
|---|---|---|---|---|---|
| 1 | **Read the plan's own meter** | The owner sees the weekly number only in the interactive status line; a headless `claude -p` run cannot pass it to its caller. | every consumer, the owner | Anthropic sends `anthropic-ratelimit-unified-{status,5h-utilization,5h-reset,7d-utilization,7d-reset}` on subscription replies; measured to survive a streamed pass-through and a 429 (architecture.md, spike). | v0.1 |
| 2 | **Per-consumer weekly share** | One program can burn the week. | the owner | Private measurement: automation's subscription traffic, priced at API rates, is several times the owner's paid API spend over the same week. | v0.1 (measured), v0.2 (enforced) |
| 3 | **Attribution under a coarse meter** | The header is account-wide and moves in steps of 1 point of the week; per-call deltas can't say who spent what. | the owner, any consumer reading its own share | Simulated week: per-call delta 5–32 points p95 error, token apportioning 0.5 when all use passes subgate, calibrated estimate 3.1–4.5 at any bypass level (ADR 0005). | v0.1 |
| 4 | **The pace line** | A fixed weekly cap lets a program spend it all on day one. allowance(t) = share × elapsed ÷ 7 days; unused allowance carries forward because counters are cumulative. | the owner | The week resets at Anthropic's `7d-reset`, not a fixed weekday; read from the header. | v0.1 (gauge), v0.2 (gate) |
| 5 | **States OK / OVER_PACE / LIMITED** | Consumers need one answer, not a calculation. LIMITED comes from a 429 or `status: rejected` and lasts until the reported reset. | consumers, dashboards | The 429 path carries the reset time (spike). | v0.1 (state), v0.2 (acts on it) |
| 6 | **Action `hold`** | A program over its share should stop and say why, in a way its existing error handling understands. An Anthropic-shaped 429 with `retry-after` and `x-subgate: over-share` or `limited`. | automation with retry or failover logic | Claude Code reports `api_error_status: 429` on such a reply (spike); clients with error failover become pace-aware with no change. | v0.2 |
| 7 | **Action `api`, with a daily dollar cap** | Some work must not wait. Move the call to the owner's paid API key, plan API credits first, under a daily cap past which it falls back to `hold`. | time-sensitive automation | Max plans include $100–200 a month of API credits that expire unused (legal.md, claim 12). | v0.2 |
| 8 | **Action `cheap`** | Work that tolerates a smaller model should not stop. Same as `api`, with the `model` field rewritten. | background jobs (summaries, refreshes) | Private measurement: one background job already moved to a small model on its own. | v0.2 |
| 9 | **The owner is measured, never gated, and fails open** | The owner's interactive sessions must never be blocked by their own tool, and must keep working when subgate is down. | the owner | Measuring the owner's traffic is what makes attribution accurate (ADR 0005 table). | v0.1 |
| 10 | **Approval requests** | Going past the line should be the owner's decision, made in a moment: "+N% this week" or "N more runs". A held consumer gets an approval id; a grant expires at the week's reset. | the owner, via any front end | The orchestrator already holds starts and asks for approval on a subscription 429. | v0.3 |
| 11 | **Runtime-editable limits, defaults in git** | Shares change mid-week; a redeploy to change a number is too slow. `PUT /v1/consumers/{name}` overrides the git file until cleared; the status shows the drift from git. | the owner, consoles | The orchestrator's console already edits server-kept options this way. | v0.2 (API), v0.3 (console) |
| 12 | **Prometheus gauges and `/v1/status`** | Dashboards and admission logic (another program deciding whether to start a job) need the numbers. | Grafana, orchestrators | — | v0.1 |
| 13 | **Audit log** | "Why was this held?" and "who raised the share?" need an answer after the fact. Append-only: state changes, actions taken, policy edits with the caller. | the owner | — | v0.2 |
| 14 | **Ledger mode** | Pacing without being in the request path, for clients that can ask first, and as the fallback if the terms rule out a proxy (legal.md). `POST /v1/admit` before a job, usage from telemetry after. | orchestrators, the stop condition | Usage Policy line effective 2026-11-12 (legal.md, claim 3). | v0.3 |
| 15 | **A second provider** | OpenAI and Codex plans have the same weekly-pool problem. Same engine, a new provider adapter. | other users | UNKNOWN which headers those plans expose; checked in that milestone. | after v0.3 |

## Out of scope, and why

| Not doing | Why |
|---|---|
| Hosting for other people, multiple owners, account pooling or rotation | Forbidden by the terms (legal.md); rotation is also what Anthropic enforces against. |
| Storing or refreshing OAuth tokens, a login flow | subgate forwards the client's credential; holding it is the "intermediate credentials" line. |
| Dollar budgets for subscription traffic | Subscription traffic isn't billed per token. Dollar caps apply only to the `api` and `cheap` fallbacks. |
| Replacing a general LLM gateway (routing, keys for many providers, caching) | Bifrost and LiteLLM do that; subgate sits in front of one and holds no provider keys in the recommended setup (ADR 0003). |
| Prompt or response logging | Not needed for metering; a privacy liability. Only token counts and model names are recorded. |
| Rewriting client identity headers | The Usage Policy names "misrepresent the product or client" (legal.md, claim 3). |

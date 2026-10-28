# Terms and legal position

Read 2026-10-28 and 2026-10-29. Not legal advice. Anthropic can change any of this; re-read the sources before each milestone.

## Verdict

subgate is software you run yourself, for your own subscription, on your own login, for your own programs. It sits **beside** the subscription, not in front of it: your programs call Anthropic directly with the unmodified Claude Code or Agent SDK, and only tell subgate what they are about to do and what they used (ADR 0003). No request and no credential passes through it.

That keeps it on the documented side of every line below. Your own `claude -p` and Agent SDK automation on your own login is documented as drawing from your plan (claim 7). The data subgate reads (the `rate_limit_event`, the `/usage` report, the status line's `rate_limits`, each run's cost) is data Anthropic hands to those clients for this purpose (ADR 0012). What stays UNKNOWN is outside subgate: how much unattended automation still counts as "ordinary, individual usage" (claim 4). subgate's job is to keep that use paced and visible.

A hosted or shared deployment, where one subscription serves other people, is **forbidden** and out of scope.

The line that shaped this design is in the Usage Policy effective 2026-11-12: it forbids "services that route requests through consumer subscriptions" (claim 3). A proxy, even one serving only its owner, might be read as one. subgate was redesigned on 2026-10-29 to route nothing (ADR 0010).

## Claims

Status: CONFIRMED (the source says it), PARTIALLY CONFIRMED, UNKNOWN (no source settles it), REJECTED.

| # | Claim | Source (fetched 2026-10-28) | Status |
|---|---|---|---|
| 1 | The Consumer Terms (effective 2025-10-08) forbid access "through automated or non-human means, whether through a bot, script, or otherwise", except through an API key or where Anthropic explicitly permits it. | [Consumer Terms](https://www.anthropic.com/legal/consumer-terms) | CONFIRMED (raw page text) |
| 2 | The Consumer Terms forbid sharing account login information or making the account available to anyone else. | same | CONFIRMED (raw page text) |
| 3 | The Usage Policy, effective 2026-11-12, forbids to "resell, proxy, or otherwise provide access to Claude through unauthorized means, including services that route requests through consumer subscriptions or misrepresent the product or client being used". | [Usage Policy](https://www.anthropic.com/legal/aup) | CONFIRMED (raw page text). Whether a single-owner, self-hosted proxy is a "service" that "provides access" is UNKNOWN. |
| 4 | Plan limits "assume ordinary, individual usage of Claude Code and the Agent SDK". | [Claude Code legal and compliance](https://code.claude.com/docs/en/legal-and-compliance) | CONFIRMED |
| 5 | Developers may not "collect, store, or intermediate Claude.ai credentials" to serve their users. | same | CONFIRMED |
| 6 | This does not restrict an end user signing in to the unmodified Claude Code binary with their own subscription. | same | CONFIRMED |
| 7 | Signed in with a Claude plan, "the Claude Agent SDK and `claude -p` still draw from your subscription limits"; Anthropic "paused the previously-announced changes" to Agent SDK usage. | [Use the Agent SDK with your Claude plan](https://support.claude.com/en/articles/15036540) | CONFIRMED. The pause is revocable. |
| 8 | The claim (from OpenClaw) that since 2026-06-15 the Agent SDK and `claude -p` draw from the subscription allowance. | claim 7 | PARTIALLY CONFIRMED. A change for 2026-06-15 was announced and then paused; SDK and `-p` use on a plan login has drawn from plan limits throughout. |
| 9 | With only `ANTHROPIC_BASE_URL` set, the saved claude.ai login stays the active credential and subscription limits apply; a gateway must forward the OAuth beta capability in `anthropic-beta`. | [LLM gateway](https://code.claude.com/docs/en/llm-gateway) | CONFIRMED. Documented for organisations' gateways. |
| 10 | Gateways that use `ANTHROPIC_AUTH_TOKEN`, `ANTHROPIC_API_KEY` or `apiKeyHelper` are billed per token to whoever owns that credential and replace the subscription. | same | CONFIRMED |
| 11 | Third-party developers may not offer claude.ai login or plan rate limits for their products. | [Agent SDK overview](https://code.claude.com/docs/en/agent-sdk/overview) | CONFIRMED |
| 12 | Max plans include monthly API credits ($100 on Max 5x, $200 on Max 20x), claimed by linking one Console organisation; they cover the API and the Agent SDK with an API key, not interactive Claude Code; unused credits expire each billing cycle. | [Monthly API credits](https://support.claude.com/en/articles/17154008), [API credits for subscribers](https://platform.claude.com/docs/en/about-claude/api-credits-for-subscribers) | CONFIRMED |
| 13 | Anthropic's trademarks need permission, and their use must not imply endorsement. Nominative use in open-source READMEs is not addressed. | [Trademark guidelines](https://www.anthropic.com/legal/trademark-guidelines) | CONFIRMED; nominative use UNKNOWN |
| 14 | Reports of a 2026 ban on third-party tools using subscription OAuth, its enforcement and a reversal. | secondary press only | UNKNOWN. No Anthropic statement found. |
| 15 | Accounts banned for running personal proxies. | blogs and issues only | UNKNOWN |
| 16 | The status line command receives the plan's `rate_limits` (5-hour and 7-day `used_percentage`, `resets_at`) for Pro and Max subscribers. | [Status line](https://code.claude.com/docs/en/statusline) (fetched 2026-10-29) | CONFIRMED |
| 17 | Agent SDK output carries a `rate_limit_event` with `status`, `utilization` and `resetsAt`; `/usage` sent as a prompt returns a structured `usage_report` with the plan's meters (marked experimental). | [TypeScript SDK](https://code.claude.com/docs/en/agent-sdk/typescript), [Python SDK](https://code.claude.com/docs/en/agent-sdk/python) (fetched 2026-10-29) | CONFIRMED |
| 18 | A `-p` run's JSON result carries `total_cost_usd` and a per-model breakdown "so scripted callers can track spend". | [Headless](https://code.claude.com/docs/en/headless) (fetched 2026-10-29) | CONFIRMED |

## The questions

| Use | Status | Why |
|---|---|---|
| Your own Agent SDK or `claude -p` automation on your own login | Allowed, within "ordinary, individual usage" | Claims 6 and 7 document it. Heavy unattended parallel use measured against "ordinary" is undefined. Claim 1 conflicts literally and is softened by 7 ("where we otherwise explicitly permit it"). |
| subgate's ledger reading what those clients already receive, and pacing them | No rule found against it; nothing is routed or intermediated | Claims 3, 5 and 11 concern routing requests and handling credentials, which the ledger doesn't do. |
| The `/usage` sampler: a small Claude Code run on your own login every 30 minutes | Same status as your other automation (row 1) | It runs the unmodified client with a documented command (ADR 0012). |
| A self-hosted proxy forwarding your own token | UNKNOWN; not built (ADR 0010) | Claim 3, read broadly, covers it. |
| Publishing the code | No rule found against it | Keep "Claude" and "Anthropic" out of the name; never imply endorsement (claim 13). |
| A hosted or shared service | Forbidden | Claims 2, 3, 5 and 11. |

## Stop condition

The project stops, and the README says so within a week, if any of these happens:

- Anthropic states that pacing one's own automation from the clients' own usage data is not permitted, or removes that data from the clients.
- Anthropic resumes the paused change in claim 7 in a way that ends plan billing for the Agent SDK or `claude -p`. (The tool may still be useful for API credits; reassess.)

## What subgate does, and never does

These are design rules, enforced by tests where a test can enforce them.

- It runs for one owner. Every consumer is the owner's own program. There is no sign-up, no multi-tenant mode, no way to add someone else's subscription.
- It is never in the request path. Clients call Anthropic directly.
- It holds no credential: no OAuth token, no API key, no gateway key. It never reads `~/.claude/.credentials.json`. Its own tokens (ADR 0006) give no access to Anthropic.
- It never changes how a client identifies itself. `subgate run` starts the command unchanged and passes the decision to it as environment variables (`SUBGATE_DECISION`, `SUBGATE_MODEL`); the command's own configuration picks its credential and model.
- It never helps get past a limit. It holds, or points the job at the owner's own API key or a cheaper model on it. It never rotates accounts or tokens.

## README notice

The README carries this notice verbatim, with `docs/legal.md` as a link:

> subgate is independent software, not affiliated with or endorsed by Anthropic. Run it yourself, for your own subscription, on your own login, for your own programs. Never host it for other people or share a subscription through it. Whether your use complies with Anthropic's Consumer Terms and Usage Policy is yours to check; see `docs/legal.md`.

## Not read in full

The full text of the Consumer Terms §2–3 and of the Usage Policy was checked for the quoted lines only. No USPTO or EUIPO search was made for the project name.

# Terms and legal position

Read 2026-10-28. Not legal advice. Anthropic can change any of this; re-read the sources before each milestone.

## Verdict

subgate is software you run yourself, for your own subscription, on your own login, for your own clients. Nothing in Anthropic's terms clearly forbids that, and nothing clearly allows it either: the status is **UNKNOWN**. A hosted or shared deployment, where one subscription serves other people, is **forbidden** and is out of scope for this project.

The closest rule is a new line in the Usage Policy that takes effect on 2026-11-12. It forbids "services that route requests through consumer subscriptions" as one way to "resell, proxy, or otherwise provide access to Claude through unauthorized means". Read narrowly, it targets services that give *other people* access. Read broadly, it covers any proxy in the request path of a subscription, including one that serves only its owner. Because of this line, the engine is built so the request path is optional (ADR 0010): the same pacing can run as a ledger that clients ask before they start, with no subgate in the request path.

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

## The four questions

| Use | Status | Why |
|---|---|---|
| Your own Agent SDK or `claude -p` automation on your own login | Allowed, within "ordinary, individual usage" | Claims 6 and 7 document it. Heavy unattended parallel use measured against "ordinary" is undefined. Claim 1 conflicts literally and is softened by 7 ("where we otherwise explicitly permit it"). |
| A self-hosted proxy forwarding your own token for your own clients | UNKNOWN | Claims 5 and 11 target third parties serving others, which this is not. Claim 9 documents a base-URL gateway that keeps the login. Claim 3, read broadly, covers it. |
| Publishing the code | UNKNOWN, no rule found against it | Keep "Claude" and "Anthropic" out of the name; never imply endorsement (claim 13). |
| A hosted or shared service | Forbidden | Claims 2, 3, 5 and 11. |

## Stop condition

The project stops, and the README says so within a week, if any of these happens:

- Anthropic states that a personal proxy in front of one's own subscription is not permitted, in its terms, docs or support articles.
- The Usage Policy line in claim 3 is clarified to cover single-owner, self-hosted use.
- Anthropic resumes the paused change in claim 7 in a way that ends plan billing for the Agent SDK or `claude -p`. (The tool may still be useful for API credits; reassess.)

Until then the ledger mode (ADR 0010) is the fallback that keeps the pacing without being in the request path.

## What subgate does, and never does

These are design rules, enforced by tests where a test can enforce them.

- It runs for one owner. Every consumer is the owner's own program. There is no sign-up, no multi-tenant mode, no way to add someone else's subscription.
- It forwards the client's credential and never stores or logs it. It has no login flow and never reads `~/.claude/.credentials.json`.
- It never changes the client's identity: `user-agent`, `x-app`, `anthropic-beta` and the client's other headers pass unchanged (claim 3's "misrepresent the product or client being used"). The `cheap` action rewrites only the `model` field, and only on a request it moves to an API key.
- It never helps get past a limit. On overrun it holds (429), moves the call to the owner's own API key, or moves it to a cheaper model on that key. It never rotates accounts or tokens.
- It uses the unmodified Claude Code binary and Agent SDK as clients.

## README notice

The README carries this notice verbatim:

> subgate is independent software, not affiliated with or endorsed by Anthropic. Run it yourself, for your own subscription, on your own login, for your own programs. Never host it for other people or share a subscription through it. Whether your use complies with Anthropic's Consumer Terms and Usage Policy is yours to check; see [docs/legal.md](docs/legal.md).

## Not read in full

The full text of the Consumer Terms §2–3 and of the Usage Policy was checked for the quoted lines only. No USPTO or EUIPO search was made for the project name.

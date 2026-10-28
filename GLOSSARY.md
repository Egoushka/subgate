# Glossary

| Term | Meaning |
|---|---|
| **owner** | The one person whose subscription and login subgate serves. Their own interactive traffic is measured, never gated. |
| **consumer** | A program of the owner's that calls the subscription through subgate, identified by a hashed header (ADR 0006). |
| **share** | A consumer's fraction of the week's allowance, for example 25%. The owner gets the rest. |
| **window** | A limit period the plan reports: 5 hours or 7 days, each with a utilization and a reset time. |
| **utilization** | The plan's own meter for a window, 0 to 1, resolution 0.01 (one point). Account-wide. |
| **pace line** | allowance(t) = share × time elapsed in the week ÷ 7 days. |
| **OVER_PACE** | A consumer whose attributed use is above its pace line, or any consumer while the account is above the total line. |
| **LIMITED** | The plan refused (429 or `status: rejected`) until the reported reset. |
| **STALE** | No fresh limit snapshot within the configured age. |
| **action** | What happens to a consumer's request when it's over: `hold`, `api` or `cheap`. |
| **hold** | Reply 429 in Anthropic's error shape with `retry-after` and `x-subgate`. |
| **api** | Forward on the consumer's API key instead of the subscription, under a daily dollar cap. |
| **cheap** | Like `api`, with the model rewritten to a cheaper one. |
| **attribution** | Splitting the account-wide meter across consumers (ADR 0005). |
| **k** | Calibration factor: meter rise per model-weighted token, fitted in quiet hours. |
| **tap** | The side channel that copies reply bytes to the usage parser without slowing the stream. |
| **observe / enforce** | Modes: observe measures only (v0.1); enforce acts (v0.2). |
| **ledger mode** | Pacing without being in the request path: clients ask `POST /v1/admit` first (ADR 0010). |

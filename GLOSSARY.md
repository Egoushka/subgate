# Glossary

| Term | Meaning |
|---|---|
| **owner** | The one person whose subscription and login subgate serves. Their own interactive use is measured, never asked. |
| **consumer** | A program of the owner's that asks subgate before a job and reports after, identified by its own subgate token (ADR 0006). |
| **ledger** | subgate's shape: beside the subscription, not in its request path. Clients ask and report; they call Anthropic directly (ADR 0003). |
| **admit** | The question a consumer asks before a job: `POST /v1/admit`. The answer is a decision. |
| **decision** | `allow`, `hold`, `api` or `cheap`. In observe mode always `allow`, with `would` showing the enforce-mode answer. |
| **lease** | An admitted job's reservation of its estimated use, held until it reports or the lease expires. |
| **share** | A consumer's fraction of the week's allowance, for example 25%. The owner gets the rest. |
| **meter** | The plan's own usage reading for a window: utilization and reset time. Account-wide, resolution one point. |
| **window** | A limit period the plan reports: 5 hours or 7 days. |
| **signal** | A meter reading posted to subgate by a reporter. |
| **reporter** | What posts signals and usage: `subgate run`, `subgate sample`, `subgate statusline` (ADR 0012). |
| **pace line** | allowance(t) = share × time elapsed in the week ÷ 7 days. |
| **OVER_PACE** | A consumer whose attributed use plus open leases is above its pace line, or any consumer while the account is above the total line. |
| **LIMITED** | The plan refused (a 429 or `status: rejected`) until the reported reset. |
| **STALE** | No fresh meter within the configured age. |
| **hold** | Don't start; try again after `retry_after_s`. |
| **api** | Run on your own API credential instead of the plan, under a daily dollar cap. |
| **cheap** | Like `api`, with a cheaper model. |
| **attribution** | Splitting the account-wide meter across consumers (ADR 0005). |
| **k** | Calibration factor: meter rise per unit of reported weighted use, fitted in quiet hours. |
| **observe / enforce** | Modes: observe never refuses (v0.1); enforce answers with the real decision (v0.2). |

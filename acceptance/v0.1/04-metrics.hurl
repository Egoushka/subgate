# C4 and C6: the gauges the dashboards and alerts read
GET {{base_url}}/metrics
HTTP 200
[Asserts]
body matches /(?m)^subgate_utilization\{window="7d"\}/
body matches /(?m)^subgate_reset_seconds[ {]/
body matches /(?m)^subgate_share_used[ {]/
body matches /(?m)^subgate_share_allowance[ {]/
body matches /(?m)^subgate_leases_open[ {]/
body matches /(?m)^subgate_signal_age_seconds[ {]/
body matches /(?m)^subgate_k[ {]/
body matches /(?m)^subgate_admit_total[ {]/

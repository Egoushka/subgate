# WC4
GET {{base_url}}/metrics
HTTP 200
[Asserts]
body matches /(?m)^subgate_plan_fraction[ {]/
body matches /(?m)^subgate_plan_age_seconds[ {]/
body matches /(?m)^subgate_plan_reversals_total[ {]/
body matches /(?m)^subgate_api_spend_usd[ {]/
body matches /(?m)^subgate_adapter_last_write_seconds[ {]/

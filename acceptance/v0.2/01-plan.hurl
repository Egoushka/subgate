# WC1 and W9: the plan has no owner, a validity window, and tenth-steps
GET {{base_url}}/v1/plan
HTTP 200
[Asserts]
jsonpath "$.consumers" exists
jsonpath "$.consumers.owner" not exists
jsonpath "$.computed_at" exists
jsonpath "$.valid_until" exists

# WC3 and W9: the plan is published, never written
PUT {{base_url}}/v1/plan
HTTP 405

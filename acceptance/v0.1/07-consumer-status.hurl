# T20: one consumer's state, and 404 for an unknown one
GET {{base_url}}/v1/status/example-job
HTTP 200
[Asserts]
jsonpath "$.consumer" == "example-job"
jsonpath "$.state" matches "^(OK|OVER_PACE|LIMITED|STALE)$"

GET {{base_url}}/v1/status/no-such-consumer
HTTP 404

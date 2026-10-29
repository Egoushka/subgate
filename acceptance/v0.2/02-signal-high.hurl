# WC2: a 7-day meter at 0.96 sends the example job to the API
POST {{base_url}}/v1/signal
Authorization: Bearer {{signal_token}}
Content-Type: application/json
file,examples/signal-7d-high.json;
HTTP *
[Asserts]
status >= 200
status < 300

GET {{base_url}}/v1/plan
HTTP 200
[Asserts]
jsonpath "$.consumers['example-job'].api" == 1

# C3: a 7-day signal moves the meter
POST {{base_url}}/v1/signal
Authorization: Bearer {{signal_token}}
Content-Type: application/json
file,examples/signal-7d.json;
HTTP *
[Asserts]
status >= 200
status < 300

GET {{base_url}}/v1/status
HTTP 200
[Asserts]
jsonpath "$.windows['7d'].utilization" == 0.42

# T14: a signal token posts signals only
POST {{base_url}}/v1/usage
Authorization: Bearer {{signal_token}}
Content-Type: application/json
{"consumer":"example-job"}
HTTP 403

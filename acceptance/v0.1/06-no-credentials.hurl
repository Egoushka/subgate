# T15: a provider credential in a body is refused with 400
POST {{base_url}}/v1/usage
Authorization: Bearer {{consumer_token}}
Content-Type: application/json
{"consumer":"example-job","note":"sk-ant-placeholder"}
HTTP 400

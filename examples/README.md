# Examples

`config.yaml` is a complete config for `subgate serve --config examples/config.yaml`. Its tokens are placeholders for local
trials and for the acceptance checks (`scripts/smoke.sh`); every `token_sha256` in it is the SHA-256 of one of these:

| Token | Speaks for |
|---|---|
| `example-owner-token-0001` | `you`, the owner |
| `example-consumer-token-0001` | `example-job` |
| `example-automation-token-0001` | `example-automation` |
| `example-signal-token-0001` | the signal reporter: posts `/v1/signal` and nothing else |

Try it:

```bash
subgate serve --config examples/config.yaml &
curl -s -H "authorization: Bearer example-consumer-token-0001" -d '{"consumer":"example-job"}' localhost:8070/v1/admit  # gitleaks:allow
curl -s -H "authorization: Bearer example-signal-token-0001" -d @examples/signal-7d.json localhost:8070/v1/signal  # gitleaks:allow
curl -s localhost:8070/v1/status
```

`signal-7d.json` is a weekly reading of 42% whose `resets_at` is a fixed date in 2100, so the example never goes stale. A real
reporter sends the plan's own reset time.

In a container, listen on `0.0.0.0:8070` and publish the port on an interface you trust; subgate does not encrypt or rate-limit.
Real tokens come from `subgate token new`; only their hashes go in the config, and the config belongs in a private repository.

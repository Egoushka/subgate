# Acceptance checks as hurl files

Each file is the executable form of a spec's local `curl` table (`docs/specs/v0.1.md` C1 to C6 and the HTTP parts of T14, T15; `docs/specs/v0.2-weight-controller.md` WC1 to WC4 and the HTTP part of W9; the date and tenth-step arithmetic of W9 stays in its unit test). `scripts/smoke.sh` starts `subgate serve` with `examples/config.yaml`, waits for `/healthz`, and runs them with [hurl](https://hurl.dev).

| Directory | Spec | Runs when |
|---|---|---|
| `v0.1/` | "See the week" (observe-mode API) | from the first `serve` build; the CI job turns blocking when the cycle's last PR lands |
| `v0.2/` | "Pace the automation" (`GET /v1/plan`) | after v0.1; run with `SMOKE_SPEC=v0.2` |

Variables, all placeholders (the tokens are the ones printed in `examples/README.md`; never put a real token here):

| Variable | Default | Meaning |
|---|---|---|
| `base_url` | `http://localhost:8070` | where `subgate serve` listens |
| `consumer_token` | none | token of `example-job`; `scripts/smoke.sh` reads `SUBGATE_CONSUMER_TOKEN` |
| `signal_token` | none | token of the signal reporter; `SUBGATE_SIGNAL_TOKEN` |

```bash
scripts/smoke.sh                                   # v0.1, starts the binary from out/Subgate
SMOKE_SPEC=v0.2 scripts/smoke.sh                   # v0.2 plan checks
SUBGATE_URL=http://host:8070 scripts/smoke.sh      # against a running instance, starts nothing
```

A hurl assertion that cannot fail is not a check: each file asserts a status code or a value the spec names, and `scripts/smoke.sh` exits non-zero when any fails.

`examples/config.yaml` (cycle 2) must hash two placeholder tokens that `examples/README.md` prints: the CI job passes `example-consumer-token-0001` as the consumer token and `example-signal-token-0001` as the signal token.

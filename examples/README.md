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

## The client commands

`run`, `statusline` and `sample` read their settings from the environment, so a token never appears in a command line:

| Variable | Meaning | Default |
|---|---|---|
| `SUBGATE_URL` | where `subgate serve` listens | `http://127.0.0.1:8070` |
| `SUBGATE_TOKEN` | the caller's token: the consumer's for `run`, the owner's for `statusline`, the signal reporter's for `sample` | none; the command says so and stops |
| `SUBGATE_CONSUMER` | the consumer's name when `--consumer` is not given | none |
| `SUBGATE_QUEUE_DIR` | where reports wait while subgate is away (`queue-<consumer>.jsonl`, numbers and ids only) | `$XDG_STATE_HOME/subgate`, else `~/.local/state/subgate` |
| `SUBGATE_CLAUDE` | the Claude Code executable `sample` runs | `claude` |

```bash
SUBGATE_CONSUMER=example-job SUBGATE_TOKEN=example-consumer-token-0001 subgate run -- claude -p "…" --output-format stream-json --verbose  # gitleaks:allow
```

- `subgate run [--consumer <name>] [--on-unavailable run|hold] -- <command…>` asks first, runs the command with its output passed
  through, then reports the meter and the use. If subgate is away the job still runs and the report is sent by the next run;
  `--on-unavailable=hold` exits 75 instead. A `hold` decision also exits 75, and the command is not started. Other exits: 64 usage,
  78 settings, 127 command not found; otherwise the command's own exit code.
- `subgate statusline` is a Claude Code status-line command for the owner: set `"statusLine": {"type": "command", "command": "subgate statusline"}`
  in `~/.claude/settings.json`, with `SUBGATE_TOKEN` and `SUBGATE_CONSUMER` of the owner in the environment Claude Code starts in. It reports the
  session's meter and cost, then prints `<model> | week 42% used | you 23% left`, where "left" is what is under the owner's pace line at
  this moment. It waits at most 300 ms, prints the plain `<model> | week 42% used` line when subgate is away, and always exits 0.
- `subgate sample [--once] [--interval 30m]` runs `claude -p /usage` on the owner's login and posts the meter; `--once` exits 0 when it was posted.
- `subgate token new <name>` prints a new token and the `token_sha256` for the config. Keep the token, put only the hash in the config.

In a container, listen on `0.0.0.0:8070` and publish the port on an interface you trust; subgate does not encrypt or rate-limit.
Real tokens come from `subgate token new`; only their hashes go in the config, and the config belongs in a private repository.

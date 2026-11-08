#!/usr/bin/env bash
# docs/specs/v0.1.md B2: `subgate serve` steady RSS after 10,000 admits and reports is at most 64 MB.
# And B1: `subgate statusline` wall time against that running server, median of 20 runs, is at most 150 ms (BENCH_STATUSLINE_MS).
# Starts SUBGATE_BIN on examples/config.yaml with a throwaway state file, sends BENCH_N admits (default 10000), each closed by
# a usage report that carries its lease, then reads the server's resident set size. Exit 0 only when it is within BENCH_RSS_MB (default 64).
# The budget is for the Linux binary (CI and the container): measured there it is about 27 MB; macOS counts shared pages and sizes the GC heap
# from a larger cache, so a Mac reads higher (about 80 MB) and BENCH_RSS_MB should be raised for a local run.
# B1 runs the owner's status line (SUBGATE_OWNER_TOKEN, default the placeholder in examples/README.md) on a recorded input.
# Needs: python3 (standard library only), curl.
set -uo pipefail
cd "$(dirname "$0")/.."

bin="${SUBGATE_BIN:-out/Subgate}"
n="${BENCH_N:-10000}"
limit_mb="${BENCH_RSS_MB:-64}"
limit_ms="${BENCH_STATUSLINE_MS:-150}"
owner_token="${SUBGATE_OWNER_TOKEN:-example-owner-token-0001}" # gitleaks:allow (a placeholder, see examples/README.md)
token="${SUBGATE_CONSUMER_TOKEN:-example-consumer-token-0001}"
base="http://127.0.0.1:8070"

[ -x "$bin" ] || { echo "bench: $bin is not built (dotnet publish src/Subgate -c Release -o out)" >&2; exit 2; }

work="$(mktemp -d)"
server=
trap '[ -n "$server" ] && kill "$server" 2>/dev/null; rm -rf "$work"' EXIT
sed "s|^state: subgate.db|state: $work/subgate.db|" examples/config.yaml > "$work/config.yaml"

"$bin" serve --config "$work/config.yaml" &
server=$!
for _ in $(seq 1 50); do
  curl -sf -o /dev/null "$base/healthz" && break
  kill -0 "$server" 2>/dev/null || { echo "bench: subgate serve exited before /healthz answered" >&2; exit 1; }
  sleep 0.2
done

started=$(date +%s)
python3 -I - "$base" "$n" "$token" <<'PY' || { echo "bench: the load run failed" >&2; exit 1; }
import http.client, json, sys
from urllib.parse import urlparse

base, n, token = urlparse(sys.argv[1]), int(sys.argv[2]), sys.argv[3]
conn = http.client.HTTPConnection(base.hostname, base.port)
headers = {"authorization": "Bearer " + token, "content-type": "application/json"}

def post(path, body):
    conn.request("POST", path, json.dumps(body), headers)
    response = conn.getresponse()
    data = response.read()
    if response.status != 200:
        sys.exit(f"{path} answered {response.status}: {data[:200]!r}")
    return json.loads(data)

for _ in range(n):
    admit = post("/v1/admit", {"consumer": "example-job", "estimate_usd": 0.01})
    post("/v1/usage", {"consumer": "example-job", "lease_id": admit["lease_id"], "cost_usd": 0.01})
PY
elapsed=$(( $(date +%s) - started ))

rss_kb="$(ps -o rss= -p "$server" | tr -d ' ')"
rss_mb=$(( rss_kb / 1024 ))
echo "B2: RSS ${rss_mb} MB after ${n} admits and reports (${elapsed}s); limit ${limit_mb} MB"
failed=0
[ "$rss_mb" -le "$limit_mb" ] || failed=1

# B1: the status line is on the owner's every prompt, so its wall time (process start included) is the budget.
median_ms="$(SUBGATE_URL="$base" SUBGATE_TOKEN="$owner_token" SUBGATE_CONSUMER=you SUBGATE_QUEUE_DIR="$work" \
  python3 -I - "$bin" <<'PY'
import os, statistics, subprocess, sys, time

line = '{"session_id":"bench-session","model":{"display_name":"Model A"},"cost":{"total_cost_usd":0.4},"rate_limits":{"five_hour":{"used_percentage":20,"resets_at":4102444800},"seven_day":{"used_percentage":41.5,"resets_at":4102444800}}}'
times = []
for _ in range(20):
    started = time.perf_counter()
    done = subprocess.run([sys.argv[1], "statusline"], input=line, text=True, capture_output=True, env=os.environ)
    times.append((time.perf_counter() - started) * 1000)
    if done.returncode != 0 or not done.stdout.strip():
        sys.exit("statusline failed: exit %d" % done.returncode)
print(int(statistics.median(times)))
PY
)" || { echo "bench: the statusline run failed" >&2; exit 1; }
echo "B1: statusline median ${median_ms} ms over 20 runs; limit ${limit_ms} ms"
[ "$median_ms" -le "$limit_ms" ] || failed=1
exit "$failed"

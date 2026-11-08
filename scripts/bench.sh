#!/usr/bin/env bash
# docs/specs/v0.1.md B2: `subgate serve` steady RSS after 10,000 admits and reports is at most 64 MB.
# Starts SUBGATE_BIN on examples/config.yaml with a throwaway state file, sends BENCH_N admits (default 10000), each closed by
# a usage report that carries its lease, then reads the server's resident set size. Exit 0 only when it is within BENCH_RSS_MB (default 64).
# The budget is for the Linux binary (CI and the container): measured there it is about 27 MB; macOS counts shared pages and sizes the GC heap
# from a larger cache, so a Mac reads higher (about 80 MB) and BENCH_RSS_MB should be raised for a local run.
# Needs: python3 (standard library only), curl.
set -uo pipefail
cd "$(dirname "$0")/.."

bin="${SUBGATE_BIN:-out/Subgate}"
n="${BENCH_N:-10000}"
limit_mb="${BENCH_RSS_MB:-64}"
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
[ "$rss_mb" -le "$limit_mb" ]

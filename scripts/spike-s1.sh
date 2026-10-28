#!/usr/bin/env bash
# Spike S1 (docs/specs/v0.1.md): does `claude -p "/usage"` give a usage_report at no model cost,
# and how often does a normal -p run emit rate_limit_event? Run on the owner's own login.
# Raw output holds real usage numbers: it stays in $OUT (default: a temp dir) and is never committed.
# Prints counts and pass/fail only.
set -euo pipefail

OUT="${OUT:-$(mktemp -d)}"
command -v claude >/dev/null || { echo "claude not found" >&2; exit 2; }
command -v jq >/dev/null || { echo "jq not found" >&2; exit 2; }

echo "claude $(claude --version | head -1)"

claude -p "/usage" --output-format stream-json --verbose > "$OUT/usage.jsonl" 2> "$OUT/usage.err" || true
rows=$(jq -s '[.[] | .usage_report?.rate_limits?.limits? // empty | length] | max // 0' "$OUT/usage.jsonl")
kinds=$(jq -rs '[.[] | .usage_report?.rate_limits?.limits? // empty | .[].kind] | unique | join(",")' "$OUT/usage.jsonl")
cost=$(jq -s '[.[] | select(.type == "result") | .total_cost_usd] | last // "none"' "$OUT/usage.jsonl")
echo "usage: limits rows=$rows kinds=${kinds:-none} total_cost_usd=$cost"

claude -p "Reply with the single word OK." --model haiku --output-format stream-json --verbose \
  > "$OUT/normal.jsonl" 2> "$OUT/normal.err" || true
events=$(jq -s '[.[] | select(.type == "rate_limit_event")] | length' "$OUT/normal.jsonl")
types=$(jq -rs '[.[] | select(.type == "rate_limit_event") | .rate_limit_info | (.rateLimitType // .rate_limit_type // "untyped")] | unique | join(",")' "$OUT/normal.jsonl")
echo "normal run: rate_limit_event=$events types=${types:-none}"
echo "raw output: $OUT"

if [ "$rows" -ge 2 ] && [ "$cost" = "0" ]; then
  echo "S1 PASS"
else
  echo "S1 FAIL"
  exit 1
fi

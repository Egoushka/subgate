#!/usr/bin/env bash
# Runs the repository's test suite and prints each check id (the test's fully
# qualified name, which follows the spec's component-and-name convention,
# e.g. "cli.RunReportsUsage" for T11) with pass or fail.
set -uo pipefail

cd "$(dirname "$0")/.."

output=$(dotnet test --logger "console;verbosity=normal" 2>&1)
status=$?

echo "$output"
echo
echo "## checks"

pass=0
fail=0
while IFS= read -r line; do
  if [[ "$line" =~ ^[[:space:]]*Passed[[:space:]]+([^[:space:]]+) ]]; then
    echo "PASS ${BASH_REMATCH[1]}"
    pass=$((pass + 1))
  elif [[ "$line" =~ ^[[:space:]]*Failed[[:space:]]+([^[:space:]]+) ]]; then
    echo "FAIL ${BASH_REMATCH[1]}"
    fail=$((fail + 1))
  fi
done <<< "$output"

echo
echo "$pass passed, $fail failed"

if [ "$status" -ne 0 ] || [ "$fail" -ne 0 ]; then
  exit 1
fi

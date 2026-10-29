#!/usr/bin/env bash
# H2 (docs/specs/v0.1.md): grep the tracked files for the private-terms list
# (hostnames, key names, addresses) and fail on any match. The list itself must
# be present and non-empty - otherwise the check can't tell a clean repo from a
# broken config, so it fails instead of passing silently.
set -uo pipefail

cd "$(dirname "$0")/.."

terms_file=".private-terms"

if [ -s "$terms_file" ]; then
  terms=$(grep -vE '^[[:space:]]*(#|$)' "$terms_file")
elif [ -n "${PRIVATE_TERMS:-}" ]; then
  terms=$(printf '%s\n' "$PRIVATE_TERMS" | grep -vE '^[[:space:]]*(#|$)')
else
  echo "private-terms-check: no list found (local $terms_file or \$PRIVATE_TERMS in CI); refusing to pass" >&2
  exit 1
fi

if [ -z "$terms" ]; then
  echo "private-terms-check: the private-terms list is empty; refusing to pass" >&2
  exit 1
fi

found=0
while IFS= read -r term; do
  [ -z "$term" ] && continue
  matches=$(git grep -niF -- "$term" -- . ":!$terms_file" 2>/dev/null) || true
  if [ -n "$matches" ]; then
    echo "$matches"
    found=1
  fi
done <<< "$terms"

if [ "$found" -ne 0 ]; then
  echo "private-terms-check: found matches against the private-terms list" >&2
  exit 1
fi

echo "private-terms-check: no matches"

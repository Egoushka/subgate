#!/usr/bin/env bash
# H3 (docs/specs/v0.1.md): every `sha256: ...` value in examples/config.yaml must
# be the sha256 of a placeholder token printed (backtick-quoted) in
# examples/README.md. Passes trivially until examples/config.yaml exists (it is
# added with the store/policy work, issue 4). Run with --self-test to check that
# this script itself fails on a mismatched hash, using throwaway fixtures.
set -uo pipefail

check_hashes() {
  local config="$1" readme="$2"
  local failures=0

  if [ ! -f "$config" ]; then
    echo "example-hashes: $config does not exist yet; nothing to check"
    return 0
  fi

  if [ ! -f "$readme" ]; then
    echo "example-hashes: $config exists but $readme is missing" >&2
    return 1
  fi

  local tokens
  tokens=$(grep -oE '`[A-Za-z0-9._-]{8,}`' "$readme" | tr -d '`')

  local hash
  while IFS= read -r hash; do
    [ -z "$hash" ] && continue
    local found=0
    local token
    while IFS= read -r token; do
      [ -z "$token" ] && continue
      if [ "$(printf '%s' "$token" | sha256sum | cut -d' ' -f1)" = "$hash" ]; then
        found=1
        break
      fi
    done <<< "$tokens"
    if [ "$found" -ne 1 ]; then
      echo "example-hashes: no placeholder token in $readme hashes to $hash" >&2
      failures=$((failures + 1))
    fi
  done < <(grep -oE 'sha256:[[:space:]]*"?[0-9a-f]{64}"?' "$config" | grep -oE '[0-9a-f]{64}')

  if [ "$failures" -ne 0 ]; then
    return 1
  fi
  echo "example-hashes: every hash in $config matches a placeholder token in $readme"
  return 0
}

self_test() {
  local dir
  dir=$(mktemp -d)
  trap 'rm -rf "$dir"' RETURN

  local token="placeholder-example-token-0000000000"
  local hash
  hash=$(printf '%s' "$token" | sha256sum | cut -d' ' -f1)

  cat > "$dir/README.md" <<EOF
Placeholder token: \`$token\`
EOF

  cat > "$dir/config.yaml" <<EOF
consumers:
  - name: example-job
    token_sha256: "$hash"
EOF

  if ! check_hashes "$dir/config.yaml" "$dir/README.md" >/dev/null; then
    echo "self-test: FAIL - a matching hash was rejected" >&2
    return 1
  fi
  echo "self-test: matching hash passes - PASS"

  sed -i "s/$hash/0000000000000000000000000000000000000000000000000000000000000000/" "$dir/config.yaml"

  if check_hashes "$dir/config.yaml" "$dir/README.md" >/dev/null 2>&1; then
    echo "self-test: FAIL - a mismatched hash was accepted" >&2
    return 1
  fi
  echo "self-test: mismatched hash fails - PASS"
}

main() {
  cd "$(dirname "$0")/.."

  if [ "${1:-}" = "--self-test" ]; then
    self_test
    exit $?
  fi

  check_hashes "examples/config.yaml" "examples/README.md"
}

main "$@"

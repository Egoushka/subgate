#!/usr/bin/env bash
# Downloads the pinned switchknife commit (external/pace.ref) into external/switchknife, which
# src/Subgate/Subgate.csproj builds Switchknife.Pace from until pace/v0.1.0 is on nuget.org (ADR 0004).
# A tarball, not a clone: no git state to keep in sync. Idempotent.
set -euo pipefail
cd "$(dirname "$0")/.."

repository=$(sed -n 's/^repository=//p' external/pace.ref)
commit=$(sed -n 's/^commit=//p' external/pace.ref)
dir=external/switchknife

if [ "$(cat "$dir/.pace-commit" 2>/dev/null)" = "$commit" ]; then
  echo "fetch-pace: $dir is at ${commit:0:7}"
  exit 0
fi

rm -rf "$dir"
mkdir -p "$dir"
curl -sSfL "${repository%.git}/archive/$commit.tar.gz" | tar -xz --strip-components=1 -C "$dir"
echo "$commit" > "$dir/.pace-commit"
echo "fetch-pace: $dir is at ${commit:0:7}"

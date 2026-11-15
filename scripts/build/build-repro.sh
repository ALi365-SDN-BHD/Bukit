#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/../.."

[[ $# -ge 2 && $# -le 3 ]] || {
  echo "usage: bash scripts/build/build-repro.sh <version> <rid> [configuration]" >&2
  exit 2
}

version="$1"
rid="$2"
configuration="${3:-Release}"

# A nondeterministic link can match by chance, so callers that need stronger
# evidence than one pair raise the number of clean publishes.
rounds="${BUKIT_REPRO_ROUNDS:-2}"
[[ "$rounds" =~ ^[1-9][0-9]?$ && "$rounds" -ge 2 && "$rounds" -le 10 ]] || {
  echo "BUKIT_REPRO_ROUNDS must be an integer from 2 to 10" >&2
  exit 2
}

# An already packaged publish tree, such as the one a release uploads, can be
# the baseline so that the shipped files themselves are covered.
reference="${BUKIT_REPRO_REFERENCE:-}"
[[ -z "$reference" || ( -d "$reference" && ! -L "$reference" ) ]] || {
  echo "BUKIT_REPRO_REFERENCE must be an existing publish directory" >&2
  exit 2
}
[[ -z "$reference" ]] || reference="$(cd "$reference" && pwd -P)"

scratch="$(mktemp -d "${TMPDIR:-/tmp}/bukit-build-repro.XXXXXX")"
trap 'rm -rf -- "$scratch"' EXIT

export GITHUB_SHA="${GITHUB_SHA:-$(git rev-parse HEAD)}"
export SOURCE_DATE_EPOCH="${SOURCE_DATE_EPOCH:-$(git show -s --format=%ct HEAD)}"

for ((run = 1; run <= rounds; run++)); do
  # These publishes are scratch evidence, not a workflow step output.
  GITHUB_OUTPUT="" bash scripts/build/package-native-aot.sh "$version" "$rid" \
    "$scratch/run-$run" "$configuration" > "$scratch/run-$run.archive"
done

baseline="${reference:-$scratch/run-1/publish/$rid}"
for ((run = 1; run <= rounds; run++)); do
  [[ "$scratch/run-$run/publish/$rid" != "$baseline" ]] || continue
  python3 scripts/build/compare-publish-trees.py \
    "$baseline" "$scratch/run-$run/publish/$rid"
done

printf 'Native AOT publish trees are reproducible: version=%s rid=%s configuration=%s rounds=%s reference=%s\n' \
  "$version" "$rid" "$configuration" "$rounds" "${reference:-none}"

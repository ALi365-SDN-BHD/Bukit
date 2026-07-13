#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/../.."

scratch="$(mktemp -d "${TMPDIR:-/tmp}/bukit-build-repro-self-test.XXXXXX")"
trap 'rm -rf -- "$scratch"' EXIT

fake_bin="$scratch/bin"
mkdir -p "$fake_bin"

cat > "$fake_bin/dotnet" <<'FAKE_DOTNET'
#!/usr/bin/env bash
set -euo pipefail

state="${FAKE_BUILD_STATE:?}"
count=0
if [[ -f "$state" ]]; then
  count="$(cat "$state")"
fi
count=$((count + 1))
printf '%s\n' "$count" > "$state"

output=""
source_revision=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    -o)
      output="${2:-}"
      shift 2
      ;;
    -p:SourceRevisionId=*)
      source_revision="${1#*=}"
      shift
      ;;
    *)
      shift
      ;;
  esac
done
[[ -n "$output" ]] || { echo "fake dotnet: missing -o" >&2; exit 71; }
[[ -n "$source_revision" ]] || { echo "fake dotnet: missing SourceRevisionId" >&2; exit 72; }
printf '%s|%s|%s\n' "$GITHUB_SHA" "$SOURCE_DATE_EPOCH" "$source_revision" \
  >> "${FAKE_BUILD_ENV_LOG:?}"

mkdir -p "$output/assets"
printf 'asset\n' > "$output/assets/data.txt"
if [[ "${FAKE_BUILD_MODE:?}" == "drift" && "$count" -eq 2 ]]; then
  printf 'second\n' > "$output/bukit"
else
  printf 'stable\n' > "$output/bukit"
fi
FAKE_DOTNET

chmod +x "$fake_bin/dotnet"
export PATH="$fake_bin:$PATH"
export FAKE_BUILD_STATE="$scratch/build-state"
export FAKE_BUILD_ENV_LOG="$scratch/build-env.log"

if bash scripts/build/build-repro.sh 1.2.3 \
  >"$scratch/missing.stdout" 2>"$scratch/missing.stderr"; then
  echo "build-repro self-test: missing RID unexpectedly succeeded" >&2
  exit 1
fi

export FAKE_BUILD_MODE=stable
rm -f -- "$FAKE_BUILD_STATE" "$FAKE_BUILD_ENV_LOG"
bash scripts/build/build-repro.sh 1.2.3 linux-x64 Release \
  >"$scratch/stable.stdout" 2>"$scratch/stable.stderr"
[[ "$(wc -l < "$FAKE_BUILD_ENV_LOG" | tr -d ' ')" == "2" ]] || {
  echo "build-repro self-test: stable run did not publish exactly twice" >&2
  exit 1
}
[[ "$(sed -n '1p' "$FAKE_BUILD_ENV_LOG")" == "$(sed -n '2p' "$FAKE_BUILD_ENV_LOG")" ]] || {
  echo "build-repro self-test: builds did not share commit/time/property" >&2
  exit 1
}

export FAKE_BUILD_MODE=drift
rm -f -- "$FAKE_BUILD_STATE" "$FAKE_BUILD_ENV_LOG"
if bash scripts/build/build-repro.sh 1.2.3 linux-x64 Release \
  >"$scratch/drift.stdout" 2>"$scratch/drift.stderr"; then
  echo "build-repro self-test: drift unexpectedly succeeded" >&2
  exit 1
fi
grep -F "changed=['bukit']" "$scratch/drift.stderr" >/dev/null

left="$scratch/left"
right="$scratch/right"
mkdir -p "$left" "$right"
printf 'same\n' > "$left/file"
printf 'same\n' > "$right/file"
python3 scripts/build/compare-publish-trees.py "$left" "$right"

ln -s file "$left/link"
if python3 scripts/build/compare-publish-trees.py "$left" "$right" \
  >"$scratch/symlink.stdout" 2>"$scratch/symlink.stderr"; then
  echo "build-repro self-test: symlink publish entry unexpectedly succeeded" >&2
  exit 1
fi
grep -F 'unsupported publish entry: link' "$scratch/symlink.stderr" >/dev/null
rm -f -- "$left/link"

mkfifo "$left/pipe"
if python3 scripts/build/compare-publish-trees.py "$left" "$right" \
  >"$scratch/special.stdout" 2>"$scratch/special.stderr"; then
  echo "build-repro self-test: special publish entry unexpectedly succeeded" >&2
  exit 1
fi
grep -F 'unsupported publish entry: pipe' "$scratch/special.stderr" >/dev/null

echo "build-repro self-test: PASS"

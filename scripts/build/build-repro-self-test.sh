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
if [[ "${FAKE_BUILD_MODE:?}" == "drift" && "$count" -eq "${FAKE_DRIFT_AT:-2}" ]]; then
  printf 'second\n' > "$output/bukit"
else
  printf 'stable\n' > "$output/bukit"
fi
chmod +x "$output/bukit"
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

export FAKE_BUILD_MODE=stable
rm -f -- "$FAKE_BUILD_STATE" "$FAKE_BUILD_ENV_LOG"
BUKIT_REPRO_ROUNDS=3 bash scripts/build/build-repro.sh 1.2.3 linux-x64 Release \
  >"$scratch/rounds.stdout" 2>"$scratch/rounds.stderr"
[[ "$(wc -l < "$FAKE_BUILD_ENV_LOG" | tr -d ' ')" == "3" ]] || {
  echo "build-repro self-test: three rounds did not publish exactly three times" >&2
  exit 1
}
grep -F 'rounds=3' "$scratch/rounds.stdout" >/dev/null

# Drift that only appears in a later round must still fail.
export FAKE_BUILD_MODE=drift
rm -f -- "$FAKE_BUILD_STATE" "$FAKE_BUILD_ENV_LOG"
if FAKE_DRIFT_AT=3 BUKIT_REPRO_ROUNDS=3 bash scripts/build/build-repro.sh 1.2.3 linux-x64 Release \
  >"$scratch/late-drift.stdout" 2>"$scratch/late-drift.stderr"; then
  echo "build-repro self-test: drift in the third round unexpectedly succeeded" >&2
  exit 1
fi
grep -F "changed=['bukit']" "$scratch/late-drift.stderr" >/dev/null

# Rounds outside 2..10 are rejected before any publish; the bounds are accepted.
export FAKE_BUILD_MODE=stable
for rounds in 1 11 02 two 2 10; do
  status=0
  rm -f -- "$FAKE_BUILD_ENV_LOG"
  BUKIT_REPRO_ROUNDS="$rounds" bash scripts/build/build-repro.sh 1.2.3 linux-x64 Release \
    >"$scratch/rounds.stdout" 2>"$scratch/rounds.stderr" || status=$?
  expected=2
  [[ "$rounds" != 2 && "$rounds" != 10 ]] || expected=0
  if [[ "$status" -ne "$expected" || ( "$expected" -eq 2 && -e "$FAKE_BUILD_ENV_LOG" ) ]]; then
    echo "build-repro self-test: BUKIT_REPRO_ROUNDS='$rounds' exited $status" >&2
    exit 1
  fi
done

# A packaged reference tree is the baseline for every fresh publish, and the
# scratch publishes never write a workflow step output.
reference="$scratch/reference"
mkdir -p "$reference/assets"
printf 'asset\n' > "$reference/assets/data.txt"
printf 'stable\n' > "$reference/bukit"
chmod +x "$reference/bukit"
rm -f -- "$FAKE_BUILD_ENV_LOG"
GITHUB_OUTPUT="$scratch/step-output" BUKIT_REPRO_REFERENCE="$reference" \
  bash scripts/build/build-repro.sh 1.2.3 linux-x64 Release \
  >"$scratch/reference.stdout" 2>"$scratch/reference.stderr"
[[ "$(wc -l < "$FAKE_BUILD_ENV_LOG" | tr -d ' ')" == "2" && ! -s "$scratch/step-output" ]] || {
  echo "build-repro self-test: reference run published wrongly or wrote step output" >&2
  exit 1
}

# Two fresh publishes that agree with each other must still fail the reference.
printf 'shipped\n' > "$reference/bukit"
if BUKIT_REPRO_REFERENCE="$reference" bash scripts/build/build-repro.sh 1.2.3 linux-x64 Release \
  >"$scratch/reference-drift.stdout" 2>"$scratch/reference-drift.stderr"; then
  echo "build-repro self-test: drift from the reference unexpectedly succeeded" >&2
  exit 1
fi
grep -F "changed=['bukit']" "$scratch/reference-drift.stderr" >/dev/null

status=0
BUKIT_REPRO_REFERENCE="$scratch/absent" bash scripts/build/build-repro.sh 1.2.3 linux-x64 Release \
  >"$scratch/absent.stdout" 2>"$scratch/absent.stderr" || status=$?
[[ "$status" -eq 2 ]] || {
  echo "build-repro self-test: missing reference exited $status" >&2
  exit 1
}

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

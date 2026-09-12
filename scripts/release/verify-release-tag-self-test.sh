#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
scratch="$(mktemp -d)"
trap 'rm -rf "$scratch"' EXIT
mkdir "$scratch/bin"
cat > "$scratch/bin/git" <<'GIT'
#!/usr/bin/env bash
set -euo pipefail
if [[ "$1" == check-ref-format ]]; then exit 0; fi
[[ "$*" == 'ls-remote origin refs/tags/v1.2.3 refs/tags/v1.2.3^{}' ]] || exit 9
[[ "$MODE" != network ]] || exit 128
a=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
b=bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
case "$MODE" in
 missing) ;;
 same) printf '%s\trefs/tags/v1.2.3\n' "$a" ;;
 mismatch) printf '%s\trefs/tags/v1.2.3\n' "$b" ;;
 annotated) printf '%s\trefs/tags/v1.2.3\n%s\trefs/tags/v1.2.3^{}\n' "$b" "$a" ;;
 annotated-mismatch) printf '%s\trefs/tags/v1.2.3\n%s\trefs/tags/v1.2.3^{}\n' "$a" "$b" ;;
 orphan) printf '%s\trefs/tags/v1.2.3^{}\n' "$a" ;;
 malformed) echo invalid ;;
 duplicate) printf '%s\trefs/tags/v1.2.3\n%s\trefs/tags/v1.2.3\n' "$a" "$a" ;;
 unexpected) printf '%s\trefs/tags/v1.2.4\n' "$a" ;;
esac
GIT
chmod +x "$scratch/bin/git"
export PATH="$scratch/bin:$PATH"
for MODE in missing same annotated; do
  export MODE
  bash "$root/scripts/release/verify-release-tag.sh" 1.2.3 aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
done
for MODE in mismatch annotated-mismatch network malformed orphan duplicate unexpected; do
  export MODE
  if bash "$root/scripts/release/verify-release-tag.sh" 1.2.3 aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa; then
    echo "unexpected success: $MODE" >&2; exit 1
  fi
done
for args in 'invalid aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa' '1.2.3 short'; do
  if bash "$root/scripts/release/verify-release-tag.sh" $args; then exit 1; fi
done
echo 'release tag self-test OK'

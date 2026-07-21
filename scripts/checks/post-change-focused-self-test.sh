#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
repo_root="$(repo_root)"
cd "$repo_root"

script="scripts/checks/post-change-focused.sh"
owner_script="scripts/checks/post-change-focused-owner-checks.sh"
owner_self_test="scripts/checks/post-change-focused-owner-checks-self-test.sh"
paths_script="scripts/checks/post-change-targeted-paths.sh"
whitespace_script="scripts/checks/untracked-whitespace.sh"
scratch=".post-change-focused-self-test.$$"
space_path=".post-change-focused self-test.$$"
output="$scratch.out"
probe_script="scripts/checks/.post-change-focused-probe.$$-self-test.sh"
probe_marker="$scratch.marker"
git_fixture="$(mktemp -d "${TMPDIR:-/tmp}/bukit-post-change-paths.XXXXXX")"
trap 'rm -f "$scratch" "$space_path" "$output" "$probe_script" "$probe_marker"; rm -rf "$git_fixture"' EXIT

fail() {
  echo "post-change focused self-test failed: $*" >&2
  exit 1
}

assert_contains() {
  case "$1" in
    *"$2"*) ;;
    *) fail "expected output to contain: $2" ;;
  esac
}

assert_not_contains() {
  case "$1" in
    *"$2"*) fail "unexpected output contains: $2" ;;
  esac
}

assert_count() {
  local actual
  actual="$(printf '%s\n' "$1" | awk -v needle="$2" '
    { line = $0; while ((at = index(line, needle)) > 0) { count++; line = substr(line, at + length(needle)); } }
    END { print count + 0 }
  ')"
  [[ "$actual" == "$3" ]] || fail "expected $3 occurrence(s) of: $2; got $actual"
}

out="$(bash "$script" --dry-run -- src/Bukit-Core/Bukit.Cli/Cli/BukitCliSpecs.cs)"
assert_contains "$out" "git diff --check HEAD -- src/Bukit-Core/Bukit.Cli/Cli/BukitCliSpecs.cs"
assert_contains "$out" "dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release"
assert_not_contains "$out" "scripts/gates/ci-fast.sh"
assert_not_contains "$out" "dotnet test bukit-test.slnx"
assert_not_contains "$out" "scripts/test-all.sh"
assert_not_contains "$out" "scripts/smoke-all.sh"

out="$(bash "$script" --dry-run -- \
  src/Bukit-Plugins/Bukit.Plugin.Echo/Bukit.Plugin.Echo.csproj \
  src/Bukit-Plugins/Bukit.Plugin.Echo/Bukit.Plugin.Echo.csproj)"
assert_count "$out" "dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release" 1
assert_count "$out" "dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj -c Release" 1

out="$(bash "$script" --dry-run --configuration Debug --base HEAD -- \
  tests/Bukit.Routing.Tests/Bukit.Routing.Tests.csproj)"
assert_contains "$out" "git diff --check HEAD -- tests/Bukit.Routing.Tests/Bukit.Routing.Tests.csproj"
assert_contains "$out" "dotnet test tests/Bukit.Routing.Tests/Bukit.Routing.Tests.csproj -c Debug"

out="$(bash "$script" --dry-run -- scripts/checks/post-change-targeted.sh)"
assert_contains "$out" "bash -n scripts/checks/post-change-targeted.sh"
assert_contains "$out" "bash scripts/checks/post-change-targeted-self-test.sh"
assert_not_contains "$out" "scripts/gates/ci-fast.sh"

out="$(bash "$script" --dry-run -- scripts/checks/post-change-focused-owner-checks.sh)"
assert_contains "$out" "bash -n scripts/checks/post-change-focused-owner-checks.sh"
assert_contains "$out" "bash scripts/checks/post-change-focused-owner-checks-self-test.sh"

out="$(bash "$script" --dry-run -- scripts/gates/ci-fast.sh)"
assert_contains "$out" "bash -n scripts/gates/ci-fast.sh"
assert_contains "$out" "bash scripts/checks/ci-fast-portability-self-test.sh"
assert_not_contains "$out" "bash scripts/gates/ci-fast.sh Release"

out="$(bash "$script" --dry-run -- .github/workflows/ci.yaml)"
assert_contains "$out" "bash scripts/checks/active-workflow-boundary-self-test.sh"
assert_contains "$out" "bash scripts/checks/active-workflow-boundary.sh"
assert_not_contains "$out" "scripts/gates/ci-fast.sh"

if bash "$script" --dry-run -- .github/workflows/release.yaml >"$output" 2>&1; then
  fail "release workflow unexpectedly passed without authorization"
fi
assert_contains "$(cat "$output")" "Refusing focused verification for blocked paths"

if bash "$script" --dry-run -- scripts/checks/no-such-owner-check.sh >"$output" 2>&1; then
  fail "unmapped owner check unexpectedly passed"
fi
assert_contains "$(cat "$output")" "No focused owner check registered"

out="$(bash "$script" --dry-run -- AGENTS.md guide/dev/agent-task-workflow.md)"
assert_count "$out" "bash scripts/checks/agent-governance-contract.sh" 1

bash "$owner_script" --dry-run
bash "$owner_self_test"

out="$(bash "$script" --dry-run -- '' 2>&1)"
assert_contains "$out" "No changed paths detected."

out="$(bash scripts/checks/post-change-targeted.sh --dry-run -- '' 2>&1)"
assert_contains "$out" "No changed paths detected."
assert_not_contains "$out" "scripts/gates/ci-fast.sh"

out="$(bash "$script" --dry-run -- \
  scripts/security/security-regression.sh \
  scripts/security/security-regression-self-test.sh)"
assert_count "$out" "bash scripts/security/security-regression-self-test.sh" 1

out="$(bash "$script" --dry-run -- \
  scripts/smoke/release-artifacts.sh \
  scripts/smoke/release-artifacts-self-test.sh)"
assert_count "$out" "bash scripts/smoke/release-artifacts-self-test.sh" 1

out="$(bash "$script" --dry-run -- \
  scripts/release/release-assets.py \
  scripts/release/release-assets-self-test.sh)"
assert_count "$out" "bash scripts/release/release-assets-self-test.sh" 1

out="$(bash "$script" --dry-run -- scripts/build/native-aot.sh scripts/build/package-native-aot.sh)"
assert_count "$out" "bash scripts/build/native-aot-self-test.sh" 1

out="$(bash "$script" --dry-run -- scripts/quality-gate.sh)"
assert_contains "$out" "bash scripts/checks/ci-fast-portability-self-test.sh"
assert_not_contains "$out" "bash scripts/gates/ci-fast.sh"

if bash "$script" --dry-run -- scripts/security/no-such-owner.py >"$output" 2>&1; then
  fail "unknown security owner path unexpectedly passed"
fi
assert_contains "$(cat "$output")" "No focused owner check registered"

printf '#!/usr/bin/env bash\nprintf created > %q\n' "$repo_root/$probe_marker" > "$probe_script"
out="$(bash "$script" --dry-run -- "$probe_script")"
assert_contains "$out" "bash $probe_script"
[[ ! -e "$probe_marker" ]] || fail "dry-run executed an owner self-test"

if bash "$script" --dry-run -- scripts/gates/ci-full.sh >"$output" 2>&1; then
  fail "blocked gate path unexpectedly passed"
fi
assert_contains "$(cat "$output")" "Refusing focused verification for blocked paths"

if out="$(bash "$script" --dry-run -- src/Bukit-Plugins/NoSuch.Plugin/File.cs 2>"$output")"; then
  fail "unmapped source unexpectedly passed"
fi
assert_contains "$out" "git diff --check HEAD -- src/Bukit-Plugins/NoSuch.Plugin/File.cs"
assert_not_contains "$out" "dotnet test"
assert_not_contains "$out" "scripts/gates/ci-fast.sh"
assert_contains "$(cat "$output")" "Cannot map these runtime source paths"

printf 'clean\n' > "$space_path"
out="$(bash "$script" --dry-run -- "$space_path")"
assert_contains "$out" "bash scripts/checks/untracked-whitespace.sh"
printf -v escaped_space_path '%q' "$space_path"
assert_contains "$out" "$escaped_space_path"

printf 'clean\n' > "$scratch"
out="$(bash "$paths_script" HEAD)"
assert_contains "$out" "$scratch"

git -C "$git_fixture" init -q
git -C "$git_fixture" config user.email self-test@example.invalid
git -C "$git_fixture" config user.name self-test
printf 'base\n' > "$git_fixture/tracked.txt"
git -C "$git_fixture" add tracked.txt
git -C "$git_fixture" commit -qm base
fixture_base="$(git -C "$git_fixture" rev-parse HEAD)"
printf 'working tree\n' > "$git_fixture/tracked.txt"
out="$(cd "$git_fixture" && bash "$repo_root/$paths_script" "$fixture_base")"
assert_contains "$out" "tracked.txt"
git -C "$git_fixture" add tracked.txt
git -C "$git_fixture" commit -qm changed
out="$(cd "$git_fixture" && bash "$repo_root/$paths_script" "$fixture_base")"
assert_contains "$out" "tracked.txt"

if bash "$paths_script" refs/heads/no-such-post-change-base >"$output" 2>&1; then
  fail "invalid discovery base unexpectedly passed"
fi
assert_contains "$(cat "$output")" "no-such-post-change-base"

printf 'clean\n' > "$scratch"
bash "$whitespace_script" "$scratch"

printf 'bad trailing whitespace \n' > "$scratch"
if bash "$whitespace_script" "$scratch" >"$output" 2>&1; then
  fail "untracked trailing whitespace unexpectedly passed"
fi
assert_contains "$(cat "$output")" "trailing whitespace"

echo "post-change focused self-test OK"

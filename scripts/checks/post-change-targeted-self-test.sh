#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
repo_root="$(repo_root)"
cd "$repo_root"

script="scripts/checks/post-change-targeted.sh"
scratch=".post-change-targeted-self-test.$$"
output="$scratch.out"
clean_fixture="$(mktemp -d "${TMPDIR:-/tmp}/bukit-targeted-clean.XXXXXX")"
trap 'rm -f "$scratch" "$output"; rm -rf "$clean_fixture"' EXIT

fail() {
  echo "post-change targeted self-test failed: $*" >&2
  exit 1
}

assert_contains() {
  case "$1" in
    *"$2"*) ;;
    *) fail "expected output to contain: $2" ;;
  esac
}

assert_not_contains() {
  case "$1" in *"$2"*) fail "unexpected output contains: $2" ;; esac
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
assert_contains "$out" "dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release"
assert_count "$out" "bash scripts/gates/ci-fast.sh Release" 1

out="$(bash "$script" --dry-run -- src/Bukit-Plugins/Bukit.Plugin.Echo/Bukit.Plugin.Echo.csproj)"
assert_contains "$out" "dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release"
assert_contains "$out" "dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj -c Release"

out="$(bash "$script" --dry-run -- tests/Bukit.Routing.Tests/Bukit.Routing.Tests.csproj)"
assert_contains "$out" "dotnet test tests/Bukit.Routing.Tests/Bukit.Routing.Tests.csproj -c Release"

out="$(bash "$script" --dry-run --configuration Debug --base HEAD -- src/Bukit-Core/Bukit.Cli/Cli/BukitCliSpecs.cs)"
assert_contains "$out" "git diff --check HEAD -- src/Bukit-Core/Bukit.Cli/Cli/BukitCliSpecs.cs"
assert_contains "$out" "dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Debug"
assert_count "$out" "bash scripts/gates/ci-fast.sh Debug" 1

out="$(bash "$script" --dry-run -- scripts/gates/ci-fast.sh)"
assert_contains "$out" "bash -n scripts/gates/ci-fast.sh"
assert_contains "$out" "bash scripts/checks/ci-fast-portability-self-test.sh"
assert_count "$out" "bash scripts/gates/ci-fast.sh Release" 1

out="$(bash "$script" --dry-run -- tests/PluginProcessProbe/Program.cs)"
assert_contains "$out" "dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj -c Release"

out="$(bash "$script" --dry-run -- tests/ThrowingPlugin/ThrowingPlugin.cs)"
assert_contains "$out" "dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release"

if bash "$script" --dry-run -- scripts/gates/ci-full.sh >"$output" 2>&1; then
  fail "blocked gate path unexpectedly passed"
fi
assert_contains "$(cat "$output")" "Refusing focused verification for blocked paths"

if out="$(bash "$script" --dry-run -- src/Bukit-Plugins/NoSuch.Plugin/File.cs 2>"$output")"; then
  fail "unmapped source unexpectedly passed"
fi
assert_contains "$out" "git diff --check HEAD -- src/Bukit-Plugins/NoSuch.Plugin/File.cs"
case "$out" in *"dotnet test"*) fail "unmapped source unexpectedly scheduled dotnet test" ;; esac
case "$out" in *"scripts/gates/ci-fast.sh"*) fail "focused failure unexpectedly continued to ci-fast" ;; esac
assert_contains "$(cat "$output")" "Cannot map these runtime source paths"

out="$(bash "$script" --dry-run -- '' 2>&1)"
assert_contains "$out" "No changed paths detected."
assert_not_contains "$out" "scripts/gates/ci-fast.sh"

mkdir -p "$clean_fixture/scripts/checks" "$clean_fixture/scripts/lib"
cp "$script" "$clean_fixture/$script"
cp scripts/checks/post-change-targeted-paths.sh "$clean_fixture/scripts/checks/"
cp scripts/lib/common.sh "$clean_fixture/scripts/lib/"
git -C "$clean_fixture" init -q
git -C "$clean_fixture" config user.email self-test@example.invalid
git -C "$clean_fixture" config user.name self-test
git -C "$clean_fixture" add .
git -C "$clean_fixture" commit -qm clean
out="$(cd "$clean_fixture" && bash "$script" --dry-run 2>&1)"
assert_contains "$out" "No changed paths detected."
assert_not_contains "$out" "scripts/gates/ci-fast.sh"

echo "post-change targeted self-test OK"

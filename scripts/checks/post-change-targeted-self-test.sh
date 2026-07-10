#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
cd "$(repo_root)"

script="scripts/checks/post-change-targeted.sh"
paths_script="scripts/checks/post-change-targeted-paths.sh"
whitespace_script="scripts/checks/untracked-whitespace.sh"
scratch=".post-change-targeted-self-test.$$"
output="$scratch.out"
trap 'rm -f "$scratch" "$output"' EXIT

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
  case "$1" in
    *"$2"*) fail "unexpected output contains: $2" ;;
  esac
}

out="$(bash "$script" --dry-run -- src/Bukit-Core/Bukit.Cli/Cli/BukitCliSpecs.cs)"
assert_contains "$out" "bash scripts/gates/ci-fast.sh Release"
assert_contains "$out" "dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release"
assert_not_contains "$out" "dotnet test bukit-test.slnx"
assert_not_contains "$out" "scripts/test-all.sh"
assert_not_contains "$out" "scripts/smoke-all.sh"

out="$(bash "$script" --dry-run -- src/Bukit-Plugins/Bukit.Plugin.Echo/Bukit.Plugin.Echo.csproj)"
assert_contains "$out" "dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release"
assert_contains "$out" "dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj -c Release"

out="$(bash "$script" --dry-run -- tests/Bukit.Routing.Tests/Bukit.Routing.Tests.csproj)"
assert_contains "$out" "dotnet test tests/Bukit.Routing.Tests/Bukit.Routing.Tests.csproj -c Release"

out="$(bash "$script" --dry-run --configuration Debug --base HEAD -- src/Bukit-Core/Bukit.Cli/Cli/BukitCliSpecs.cs)"
assert_contains "$out" "git diff --check HEAD -- src/Bukit-Core/Bukit.Cli/Cli/BukitCliSpecs.cs"
assert_contains "$out" "bash scripts/gates/ci-fast.sh Debug"
assert_contains "$out" "dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Debug"

out="$(bash "$script" --dry-run -- scripts/gates/ci-fast.sh)"
assert_contains "$out" "bash -n scripts/gates/ci-fast.sh"
assert_contains "$out" "bash scripts/gates/ci-fast.sh Release"

out="$(bash "$script" --dry-run -- tests/PluginProcessProbe/Program.cs)"
assert_contains "$out" "dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj -c Release"

out="$(bash "$script" --dry-run -- tests/ThrowingPlugin/ThrowingPlugin.cs)"
assert_contains "$out" "dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release"

if bash "$script" --dry-run -- scripts/gates/ci-full.sh >"$output" 2>&1; then
  fail "blocked gate path unexpectedly passed"
fi
assert_contains "$(cat "$output")" "Refusing targeted verification for blocked paths"

if out="$(bash "$script" --dry-run -- src/Bukit-Plugins/NoSuch.Plugin/File.cs 2>"$output")"; then
  fail "unmapped source unexpectedly passed"
fi
assert_contains "$out" "git diff --check HEAD -- src/Bukit-Plugins/NoSuch.Plugin/File.cs"
assert_contains "$out" "bash scripts/gates/ci-fast.sh Release"
assert_not_contains "$out" "dotnet test"
assert_contains "$(cat "$output")" "Cannot map these runtime source paths"

printf 'clean\n' > "$scratch"
out="$(bash "$paths_script" HEAD)"
assert_contains "$out" "$scratch"

if bash "$paths_script" refs/heads/no-such-post-change-base >"$output" 2>&1; then
  fail "invalid discovery base unexpectedly passed"
fi
assert_contains "$(cat "$output")" "no-such-post-change-base"

printf 'clean\n' > "$scratch"
bash "$whitespace_script" "$scratch"

if bash "$whitespace_script" "$scratch.missing" >"$output" 2>&1; then
  fail "missing whitespace path unexpectedly passed"
fi
assert_contains "$(cat "$output")" "Could not access"

if bash "$whitespace_script" >"$output" 2>&1; then
  fail "missing whitespace argument unexpectedly passed"
fi
assert_contains "$(cat "$output")" "usage:"

printf 'bad trailing whitespace \n' > "$scratch"
if bash "$whitespace_script" "$scratch" >"$output" 2>&1; then
  fail "untracked trailing whitespace unexpectedly passed"
fi
assert_contains "$(cat "$output")" "trailing whitespace"

echo "post-change targeted self-test OK"

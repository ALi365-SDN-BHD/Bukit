# Post-change Targeted Gate P2 Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close all four audited P2 gaps without weakening the P1 fixes or running a broad gate.

**Architecture:** Extract changed-path discovery and targeted-project mapping into focused Bash helpers so each contract is deterministic and the coordinator stays under 200 lines. Keep the coordinator responsible for ordering thin checks, mapping failures, and targeted test execution; harden the existing whitespace helper by separating Git stdout from stderr.

**Tech Stack:** Bash 3-compatible shell scripts, Git plumbing commands, repository `run_step` helpers.

## Global Constraints

- Fix all four P2 findings from `docs/superpowers/specs/2026-07-10-post-change-p2-fixes-design.md`.
- Preserve the previously fixed P1 helper-project mappings and non-recursive self-test behavior.
- Keep every active shell script at or below 200 lines.
- Do not run `ci-full`, release, `test-all`, `smoke-all`, or whole-solution `.slnx` tests.
- Do not modify `guide-0.1/`, `guide-0.2/`, `scripts-0.1/`, or `scripts-0.2/`.
- Do not change CI workflow behavior.
- Preserve unrelated staged and working-tree changes; do not commit implementation files automatically.

---

## File Structure

- Create `scripts/checks/post-change-targeted-projects.sh`: map one changed path to zero, one, or multiple targeted test projects.
- Create `scripts/checks/post-change-targeted-paths.sh`: list tracked and untracked changes relative to one base ref.
- Modify `scripts/checks/untracked-whitespace.sh`: distinguish clean differences, whitespace diagnostics, and Git/path errors.
- Modify `scripts/checks/post-change-targeted.sh`: delegate discovery and mapping, then order thin checks before unmapped-source failure.
- Modify `scripts/checks/post-change-targeted-self-test.sh`: add deterministic regressions for all four P2 behaviors.
- Keep `scripts/gates/ci-fast.sh` unchanged.

### Task 1: Extract Project Mapping and Add Echo Consumers

**Files:**
- Create: `scripts/checks/post-change-targeted-projects.sh`
- Modify: `scripts/checks/post-change-targeted.sh`
- Modify: `scripts/checks/post-change-targeted-self-test.sh`

**Interfaces:**
- Consumes: `bash scripts/checks/post-change-targeted-projects.sh PATH`.
- Produces: one project per output line; exit 1 when PATH has no mapping.

- [ ] **Step 1: Add failing Echo and ordinary-test assertions**

Add after the existing Core mapping assertions:

```bash
out="$(bash "$script" --dry-run -- src/Bukit-Plugins/Bukit.Plugin.Echo/Bukit.Plugin.Echo.csproj)"
assert_contains "$out" "dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release"
assert_contains "$out" "dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj -c Release"

out="$(bash "$script" --dry-run -- tests/Bukit.Routing.Tests/Bukit.Routing.Tests.csproj)"
assert_contains "$out" "dotnet test tests/Bukit.Routing.Tests/Bukit.Routing.Tests.csproj -c Release"
```

- [ ] **Step 2: Verify RED**

Run:

```bash
bash scripts/checks/post-change-targeted-self-test.sh
```

Expected: FAIL because Echo is mapped to the nonexistent
`tests/Bukit.Plugin.Echo.Tests/Bukit.Plugin.Echo.Tests.csproj`.

- [ ] **Step 3: Create the mapping helper**

Create `scripts/checks/post-change-targeted-projects.sh`:

```bash
#!/usr/bin/env bash
set -euo pipefail

[[ $# -eq 1 ]] || { echo "usage: bash scripts/checks/post-change-targeted-projects.sh PATH" >&2; exit 2; }

path="${1#./}"

project_for_module() {
  local module="$1"
  case "$module" in
    Bukit.Cli.Shared) module="Bukit.Cli" ;;
    Bukit.Plugin.WechatSync|Bukit.WechatSyncing) module="Bukit.Plugin.WechatSync" ;;
    Bukit.Plugin.Echo)
      printf '%s\n' \
        tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj \
        tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj
      return ;;
  esac
  printf 'tests/%s.Tests/%s.Tests.csproj\n' "$module" "$module"
}

case "$path" in
  src/Bukit-Core/*/*) module="${path#src/Bukit-Core/}" ;;
  src/Bukit-Labs/*/*) module="${path#src/Bukit-Labs/}" ;;
  src/Bukit-Plugins/*/*) module="${path#src/Bukit-Plugins/}" ;;
  tests/PluginProcessProbe/*)
    printf '%s\n' tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj
    exit 0 ;;
  tests/ThrowingPlugin/*)
    printf '%s\n' tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj
    exit 0 ;;
  tests/*.Tests/*)
    test_dir="${path#tests/}"; test_dir="${test_dir%%/*}"
    printf 'tests/%s/%s.csproj\n' "$test_dir" "$test_dir"
    exit 0 ;;
  *) exit 1 ;;
esac

module="${module%%/*}"
project_for_module "$module"
```

- [ ] **Step 4: Delegate coordinator mapping**

Delete `project_for_module`, `source_project_for_path`, and
`test_project_for_path` from `post-change-targeted.sh`. Add:

```bash
add_projects_for_path() {
  local path="$1" projects project
  projects="$(bash scripts/checks/post-change-targeted-projects.sh "$path")" || return 1
  while IFS= read -r project; do
    [[ -n "$project" ]] && add_test_project "$project" "$path"
  done <<< "$projects"
}
```

Replace the source/test mapping branch with:

```bash
if [[ "$path" == src/* ]]; then
  add_projects_for_path "$path" || unmapped_sources+=("$path")
elif [[ "$path" == tests/* ]]; then
  add_projects_for_path "$path" || true
fi
```

- [ ] **Step 5: Verify GREEN**

Run:

```bash
bash scripts/checks/post-change-targeted-self-test.sh
bash -n scripts/checks/post-change-targeted-projects.sh
bash -n scripts/checks/post-change-targeted.sh
```

Expected: self-test prints `post-change targeted self-test OK`; syntax checks
exit 0; `wc -l scripts/checks/post-change-targeted.sh` is at most 200.

### Task 2: Extract Deterministic Changed-path Discovery

**Files:**
- Create: `scripts/checks/post-change-targeted-paths.sh`
- Modify: `scripts/checks/post-change-targeted.sh`
- Modify: `scripts/checks/post-change-targeted-self-test.sh`

**Interfaces:**
- Consumes: `bash scripts/checks/post-change-targeted-paths.sh BASE`.
- Produces: one changed tracked or untracked path per line; propagates Git errors.

- [ ] **Step 1: Replace the catch-all skip with failing helper assertions**

Define:

```bash
paths_script="scripts/checks/post-change-targeted-paths.sh"
```

Replace the current default-discovery `if/else` block with:

```bash
printf 'clean\n' > "$scratch"
out="$(bash "$paths_script" HEAD)"
assert_contains "$out" "$scratch"

if bash "$paths_script" refs/heads/no-such-post-change-base >"$output" 2>&1; then
  fail "invalid discovery base unexpectedly passed"
fi
assert_contains "$(cat "$output")" "no-such-post-change-base"
```

- [ ] **Step 2: Verify RED**

Run:

```bash
bash scripts/checks/post-change-targeted-self-test.sh
```

Expected: FAIL because `post-change-targeted-paths.sh` does not exist.

- [ ] **Step 3: Create the path helper**

Create `scripts/checks/post-change-targeted-paths.sh`:

```bash
#!/usr/bin/env bash
set -euo pipefail

[[ $# -eq 1 ]] || { echo "usage: bash scripts/checks/post-change-targeted-paths.sh BASE" >&2; exit 2; }

git diff --name-only "$1" --
git ls-files --others --exclude-standard
```

- [ ] **Step 4: Delegate no-argument discovery**

Replace the two embedded Git loops with:

```bash
if [[ ${#paths[@]} -eq 0 ]]; then
  discovered_paths="$(bash scripts/checks/post-change-targeted-paths.sh "$base_ref")"
  while IFS= read -r path; do add_changed_path "$path"; done <<< "$discovered_paths"
else
  for path in "${paths[@]}"; do add_changed_path "$path"; done
fi
```

- [ ] **Step 5: Verify GREEN**

Run:

```bash
bash scripts/checks/post-change-targeted-self-test.sh
bash -n scripts/checks/post-change-targeted-paths.sh
bash scripts/checks/post-change-targeted-paths.sh refs/heads/no-such-post-change-base
```

Expected: self-test passes; syntax exits 0; the invalid base command exits
non-zero and names the invalid ref.

### Task 3: Propagate Whitespace Checker Errors

**Files:**
- Modify: `scripts/checks/untracked-whitespace.sh`
- Modify: `scripts/checks/post-change-targeted-self-test.sh`

**Interfaces:**
- Consumes: exactly one file path.
- Produces: exit 0 for no diagnostics; exit 1 for whitespace diagnostics; non-zero for path/Git errors; exit 2 for invalid argument count.

- [ ] **Step 1: Add failing missing-path and argument assertions**

After the existing trailing-whitespace assertion, add:

```bash
if bash "$whitespace_script" "$scratch.missing" >"$output" 2>&1; then
  fail "missing whitespace path unexpectedly passed"
fi
assert_contains "$(cat "$output")" "Could not access"

if bash "$whitespace_script" >"$output" 2>&1; then
  fail "missing whitespace argument unexpectedly passed"
fi
assert_contains "$(cat "$output")" "usage:"
```

Also verify a clean file before changing it to trailing whitespace:

```bash
printf 'clean\n' > "$scratch"
bash "$whitespace_script" "$scratch"
```

- [ ] **Step 2: Verify RED**

Run:

```bash
bash scripts/checks/post-change-targeted-self-test.sh
```

Expected: FAIL with `missing whitespace path unexpectedly passed`.

- [ ] **Step 3: Capture Git stdout and stderr separately**

Replace `untracked-whitespace.sh` with:

```bash
#!/usr/bin/env bash
set -euo pipefail

[[ $# -eq 1 ]] || { echo "usage: bash scripts/checks/untracked-whitespace.sh PATH" >&2; exit 2; }

stderr_file="$(mktemp "${TMPDIR:-/tmp}/bukit-untracked-whitespace.XXXXXX")"
trap 'rm -f "$stderr_file"' EXIT

rc=0
out="$(git diff --check --no-index -- /dev/null "$1" 2>"$stderr_file")" || rc=$?
err="$(cat "$stderr_file")"

if [[ -n "$err" ]]; then
  printf '%s\n' "$err" >&2
  [[ "$rc" -ne 0 ]] && exit "$rc"
  exit 2
fi
if [[ -n "$out" ]]; then
  printf '%s\n' "$out" >&2
  exit 1
fi
case "$rc" in
  0|1) exit 0 ;;
  *) exit "$rc" ;;
esac
```

- [ ] **Step 4: Verify GREEN**

Run:

```bash
bash scripts/checks/post-change-targeted-self-test.sh
bash -n scripts/checks/untracked-whitespace.sh
```

Expected: self-test prints `post-change targeted self-test OK`; syntax exits 0.

### Task 4: Run Thin Checks Before Unmapped-source Failure

**Files:**
- Modify: `scripts/checks/post-change-targeted.sh`
- Modify: `scripts/checks/post-change-targeted-self-test.sh`

**Interfaces:**
- Consumes: the coordinator's existing `unmapped_sources` array.
- Produces: thin command output followed by non-zero mapping failure; no targeted test command.

- [ ] **Step 1: Strengthen the unmapped-source regression**

Replace the existing unmapped assertion with:

```bash
if out="$(bash "$script" --dry-run -- src/Bukit-Plugins/NoSuch.Plugin/File.cs 2>"$output")"; then
  fail "unmapped source unexpectedly passed"
fi
assert_contains "$out" "git diff --check HEAD -- src/Bukit-Plugins/NoSuch.Plugin/File.cs"
assert_contains "$out" "bash scripts/gates/ci-fast.sh Release"
assert_not_contains "$out" "dotnet test"
assert_contains "$(cat "$output")" "Cannot map these runtime source paths"
```

- [ ] **Step 2: Verify RED**

Run:

```bash
bash scripts/checks/post-change-targeted-self-test.sh
```

Expected: FAIL because the current coordinator exits before printing
`git diff --check`.

- [ ] **Step 3: Move the mapping failure after thin checks**

Keep blocked-path refusal where it is. Move only the `unmapped_sources` error
block to immediately after:

```bash
run_or_print "fast contract gate" bash scripts/gates/ci-fast.sh "$configuration"
```

The moved block remains:

```bash
if [[ ${#unmapped_sources[@]} -gt 0 ]]; then
  echo "Cannot map these runtime source paths to targeted test projects:" >&2
  printf '  %s\n' "${unmapped_sources[@]}" >&2
  echo "Add a mapping or run an explicit targeted test command; no full-gate fallback is allowed." >&2
  exit 1
fi
```

- [ ] **Step 4: Verify GREEN**

Run:

```bash
bash scripts/checks/post-change-targeted-self-test.sh
bash scripts/checks/post-change-targeted.sh --dry-run -- src/Bukit-Plugins/NoSuch.Plugin/File.cs
```

Expected: self-test passes; direct dry-run prints thin commands, emits the
mapping error, exits non-zero, and prints no `dotnet test` command.

### Task 5: Scoped Integration Verification and Review

**Files:**
- Verify: `scripts/checks/post-change-targeted-projects.sh`
- Verify: `scripts/checks/post-change-targeted-paths.sh`
- Verify: `scripts/checks/untracked-whitespace.sh`
- Verify: `scripts/checks/post-change-targeted.sh`
- Verify: `scripts/checks/post-change-targeted-self-test.sh`

**Interfaces:**
- Consumes: the final scoped diff.
- Produces: targeted gate evidence plus task-level and final read-only review verdicts.

- [ ] **Step 1: Verify syntax, size, and whitespace**

Run:

```bash
bash -n scripts/checks/post-change-targeted-projects.sh
bash -n scripts/checks/post-change-targeted-paths.sh
bash -n scripts/checks/untracked-whitespace.sh
bash -n scripts/checks/post-change-targeted.sh
bash -n scripts/checks/post-change-targeted-self-test.sh
wc -l scripts/checks/post-change-targeted-projects.sh scripts/checks/post-change-targeted-paths.sh scripts/checks/untracked-whitespace.sh scripts/checks/post-change-targeted.sh scripts/checks/post-change-targeted-self-test.sh
git diff --check -- scripts/checks/post-change-targeted.sh scripts/checks/post-change-targeted-self-test.sh
```

Expected: syntax and whitespace checks exit 0; every script is at most 200
lines.

- [ ] **Step 2: Run the explicit targeted gate**

Run:

```bash
bash scripts/checks/post-change-targeted.sh -- \
  scripts/checks/post-change-targeted-projects.sh \
  scripts/checks/post-change-targeted-paths.sh \
  scripts/checks/untracked-whitespace.sh \
  scripts/checks/post-change-targeted.sh \
  scripts/checks/post-change-targeted-self-test.sh
```

Expected: all focused whitespace, shell syntax, `ci-fast`, and self-test steps
pass without a forbidden broad gate.

- [ ] **Step 3: Perform bounded reviews**

After each task, dispatch a fresh read-only reviewer for spec compliance and
code quality. After Task 4, dispatch one final high-capability reviewer over
the complete P2 diff. Resolve Critical or Important findings and rerun the same
focused verification; record Minor findings without expanding scope.

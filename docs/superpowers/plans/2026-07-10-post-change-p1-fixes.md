# Post-change Targeted Gate P1 Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ensure changes to test helper projects run their consuming test projects and remove the recursive `ci-fast` path from the post-change self-test.

**Architecture:** Keep explicit repository-contract mappings in the targeted gate. Extract untracked-file whitespace verification into a focused shell checker so the self-test exercises that behavior without invoking the complete post-change gate.

**Tech Stack:** Bash 3-compatible shell scripts, Git plumbing commands, repository `run_step` helpers.

## Global Constraints

- Fix only the two audited P1 findings.
- Do not change the existing P2 findings in this task.
- Do not run `ci-full`, release, `test-all`, `smoke-all`, or whole-solution `.slnx` tests.
- Do not modify `guide-0.1/`, `guide-0.2/`, `scripts-0.1/`, or `scripts-0.2/`.
- Keep `scripts/checks/post-change-targeted.sh` at or below the repository's 200-line active-script limit.
- Preserve unrelated staged and working-tree changes; do not commit implementation files automatically.

---

## File Structure

- Create `scripts/checks/untracked-whitespace.sh`: validate one untracked path with `git diff --check --no-index`.
- Modify `scripts/checks/post-change-targeted.sh`: add two helper-project mappings and delegate untracked whitespace checks.
- Modify `scripts/checks/post-change-targeted-self-test.sh`: add mapping regressions and test the focused whitespace checker directly.
- Keep `scripts/gates/ci-fast.sh` unchanged: it continues to run the self-test, which no longer invokes the complete targeted gate in non-dry-run mode.

### Task 1: Map Test Helper Projects to Consumer Tests

**Files:**
- Modify: `scripts/checks/post-change-targeted-self-test.sh`
- Modify: `scripts/checks/post-change-targeted.sh`

**Interfaces:**
- Consumes: explicit changed paths accepted by `post-change-targeted.sh -- <path>`.
- Produces: `tests/PluginProcessProbe/* -> Bukit.PluginHost.Tests` and `tests/ThrowingPlugin/* -> Bukit.Engine.Tests`.

- [ ] **Step 1: Write failing mapping assertions**

Add these assertions after the existing Core mapping checks:

```bash
out="$(bash "$script" --dry-run -- tests/PluginProcessProbe/Program.cs)"
assert_contains "$out" "dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj -c Release"

out="$(bash "$script" --dry-run -- tests/ThrowingPlugin/ThrowingPlugin.cs)"
assert_contains "$out" "dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release"
```

- [ ] **Step 2: Run the self-test and verify RED**

Run:

```bash
bash scripts/checks/post-change-targeted-self-test.sh
```

Expected: FAIL with `expected output to contain` for the missing
`Bukit.PluginHost.Tests` command.

- [ ] **Step 3: Add the minimal explicit mappings**

Change `test_project_for_path` to select the consumer test directory first and
print one project path:

```bash
test_project_for_path() {
  local path="$1" test_dir
  case "$path" in
    tests/PluginProcessProbe/*) test_dir="Bukit.PluginHost.Tests" ;;
    tests/ThrowingPlugin/*) test_dir="Bukit.Engine.Tests" ;;
    tests/*.Tests/*)
      test_dir="${path#tests/}"; test_dir="${test_dir%%/*}" ;;
    *) return 1 ;;
  esac
  printf 'tests/%s/%s.csproj\n' "$test_dir" "$test_dir"
}
```

Remove two non-functional blank lines elsewhere in the file if needed to keep
the script at or below 200 lines; do not alter behavior for that purpose.

- [ ] **Step 4: Run the self-test and verify GREEN**

Run:

```bash
bash scripts/checks/post-change-targeted-self-test.sh
```

Expected: `post-change targeted self-test OK`.

### Task 2: Remove the Recursive Self-test Edge

**Files:**
- Create: `scripts/checks/untracked-whitespace.sh`
- Modify: `scripts/checks/post-change-targeted-self-test.sh`
- Modify: `scripts/checks/post-change-targeted.sh`

**Interfaces:**
- Consumes: exactly one repository-relative file path.
- Produces: exit 0 with no whitespace report; exit 1 with the Git whitespace report when a violation exists.

- [ ] **Step 1: Point the regression test at the focused checker**

Define the checker next to the existing `script` variable:

```bash
whitespace_script="scripts/checks/untracked-whitespace.sh"
```

Replace the trailing-whitespace invocation with:

```bash
if bash "$whitespace_script" "$scratch" >"$output" 2>&1; then
  fail "untracked trailing whitespace unexpectedly passed"
fi
assert_contains "$(cat "$output")" "trailing whitespace"
```

- [ ] **Step 2: Run the self-test and verify RED**

Run:

```bash
bash scripts/checks/post-change-targeted-self-test.sh
```

Expected: FAIL because `scripts/checks/untracked-whitespace.sh` does not exist.

- [ ] **Step 3: Implement the focused checker**

Create `scripts/checks/untracked-whitespace.sh`:

```bash
#!/usr/bin/env bash
set -euo pipefail

[[ $# -eq 1 ]] || { echo "usage: bash scripts/checks/untracked-whitespace.sh PATH" >&2; exit 2; }

out="$(git diff --check --no-index -- /dev/null "$1" || true)"
if [[ -n "$out" ]]; then
  printf '%s\n' "$out" >&2
  exit 1
fi
```

This intentionally preserves the existing Git-error behavior because changing
that behavior is a separate audited P2.

- [ ] **Step 4: Delegate production checks to the focused checker**

Replace the inline untracked check in `post-change-targeted.sh` with:

```bash
if [[ ${#untracked_paths[@]} -gt 0 ]]; then
  for path in "${untracked_paths[@]}"; do
    run_or_print "untracked whitespace: $path" bash scripts/checks/untracked-whitespace.sh "$path"
  done
fi
```

Confirm every invocation of `post-change-targeted.sh` in the self-test includes
`--dry-run`:

```bash
rg -n 'bash "\$script"' scripts/checks/post-change-targeted-self-test.sh
```

Expected: all reported invocations include `--dry-run`; the whitespace case
uses `$whitespace_script` instead.

- [ ] **Step 5: Run focused tests and verify GREEN**

Run:

```bash
bash scripts/checks/post-change-targeted-self-test.sh
bash -n scripts/checks/untracked-whitespace.sh
bash -n scripts/checks/post-change-targeted.sh
bash -n scripts/checks/post-change-targeted-self-test.sh
```

Expected: self-test prints `post-change targeted self-test OK`; all syntax
checks exit 0.

### Task 3: Scoped Verification and Review

**Files:**
- Verify: `scripts/checks/untracked-whitespace.sh`
- Verify: `scripts/checks/post-change-targeted.sh`
- Verify: `scripts/checks/post-change-targeted-self-test.sh`

**Interfaces:**
- Consumes: the final scoped diff.
- Produces: targeted gate evidence and a read-only review verdict.

- [ ] **Step 1: Confirm line and scope boundaries**

Run:

```bash
wc -l scripts/checks/post-change-targeted.sh
git diff --check -- scripts/checks/untracked-whitespace.sh scripts/checks/post-change-targeted.sh scripts/checks/post-change-targeted-self-test.sh
git diff -- scripts/checks/untracked-whitespace.sh scripts/checks/post-change-targeted.sh scripts/checks/post-change-targeted-self-test.sh
```

Expected: targeted script is at most 200 lines; whitespace check exits 0; diff
contains only the two P1 fixes and their tests.

- [ ] **Step 2: Run the explicit targeted gate**

Run:

```bash
bash scripts/checks/post-change-targeted.sh -- \
  scripts/checks/untracked-whitespace.sh \
  scripts/checks/post-change-targeted.sh \
  scripts/checks/post-change-targeted-self-test.sh
```

Expected: shell syntax, `ci-fast`, and its post-change self-test pass. No
forbidden broad-gate command appears.

- [ ] **Step 3: Request bounded read-only review**

Dispatch one reviewer to inspect only the three scoped files. Require explicit
checks for helper mappings, recursive call paths, accidental P2 changes, and
forbidden broad-gate execution. The reviewer must not modify files.

- [ ] **Step 4: Perform final main-thread audit**

Re-read the reviewer findings and final diff. Resolve any P1 regression, rerun
the same focused verification, and report P2 findings as unchanged rather than
silently fixing them.

# Testing

## Focused affected checks

Run this after each code subtask with only that subtask's paths:

```bash
bash scripts/checks/post-change-focused.sh -- <changed paths>
```

With no explicit paths, the script discovers tracked and untracked changes
relative to `HEAD`. Use `--base <sha>` for an already committed diff and
`--dry-run` to print commands without executing them.

The focused gate runs:

- `git diff --check` and untracked-file whitespace checks;
- `bash -n` for changed shell scripts;
- registered direct owner tests or self-tests;
- only test projects mapped from affected source or test paths.

It fails on an unmapped runtime source. It does not run `ci-fast`, full/release
gates, unrelated projects, or whole-solution tests.

The focused gate and mapping contract have direct self-tests:

```bash
bash scripts/checks/post-change-focused-self-test.sh
bash scripts/checks/post-change-targeted-self-test.sh
```

## Aggregate targeted gate

Run this once after all parent-task subtasks have passed focused checks:

```bash
bash scripts/checks/post-change-targeted.sh \
  --base <parent-task-base-sha> -- <all parent-task changed paths>
```

It applies focused verification to the aggregate diff and then runs `ci-fast`
exactly once. It does not invoke full, release, smoke-all, test-all, or a
whole-solution test.

## Direct owner proof paths

For repository agent-governance and active development-documentation changes:

```bash
bash scripts/checks/agent-governance-contract.sh
bash scripts/checks/docs-consistency.sh
```

For `guide/skills/` content changes, use the Skills pack's own validators:

```bash
bash scripts/checks/skills-schema.sh
bash guide/skills/scripts/validate-skills-strict.sh
```

For CI-fast wiring and active workflow boundaries:

```bash
bash scripts/checks/ci-fast-portability-self-test.sh
bash scripts/checks/active-workflow-boundary-self-test.sh
bash scripts/checks/active-workflow-boundary.sh
```

For security and release-artifact script behavior:

```bash
bash scripts/security/security-regression-self-test.sh
bash scripts/smoke/release-artifacts-self-test.sh
bash scripts/release/release-assets-self-test.sh
bash scripts/build/native-aot-self-test.sh
bash scripts/build/build-repro-self-test.sh
bash scripts/build/normalize-yaml-static-context-self-test.sh
```

Run a real full/release owner gate only with explicit user authorization. If
authorization is absent, report the remaining verification boundary.
Unknown gate or verification paths fail focused verification until a direct
owner self-test is registered.

## Explicit broad gates

The following are never part of focused or aggregate post-change verification:

```bash
bash scripts/gates/ci-full.sh Release
bash scripts/gates/release.sh Release
bash scripts/test-all.sh
bash scripts/smoke-all.sh
dotnet test bukit-test.slnx
```

Use them only when the user explicitly requests the corresponding broad proof.

## Coverage and artifact checks

Core coverage is a separate explicit gate:

```bash
bash scripts/checks/coverage-baseline-schema.sh
bash scripts/checks/coverage.sh Release
```

Smoke one supported release archive or publish directory with:

```bash
bash scripts/smoke/release-artifacts.sh <archive-or-publish-dir> <rid>
```

Supported RIDs are `linux-x64`, `osx-arm64`, and `win-x64`. These checks are not
automatic substitutes for focused or aggregate validation.

## Failure reporting

Classify failures as scoped regressions, pre-existing failures, environment
restrictions, or infrastructure noise. Modify code only when evidence connects
the failure to the active diff. Report unresolved environment or infrastructure
failures precisely; do not change unrelated code to make a gate pass.

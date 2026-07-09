# Coverage Gate Residual Closure Design

## Scope

Close only the remaining Coverage Gate findings identified by the strict audit:

- Restrict destructive coverage cleanup to repository coverage output or a
  dedicated Bukit temporary-directory namespace.
- Replace workflow substring checks with YAML-structure assertions for coverage
  job dependencies, commands, and artifacts.
- Track the six runtime and self-test helpers currently missing from Git.
- Rewrite the unpushed local commit sequence so Coverage and post-change work
  are separate, reviewable commits while preserving all current local changes.

No Core runtime behavior, backup/reference tree, coverage thresholds, project
list, or unrelated post-change implementation is changed.

## Output Safety

`validate-output-root.py` continues to accept `TestResults/coverage` and its
descendants. A temporary output is accepted only when its first path component
below a recognized system temporary root starts with `bukit-coverage-`; that
dedicated directory and its descendants are accepted, but the system temp root
itself is not. Arbitrary temporary
directories such as `/private/tmp/unrelated-project-data` are rejected.

The path self-test must demonstrate both sides of the contract before the
implementation changes: the unrelated temporary path is rejected and a
dedicated `bukit-coverage-*` directory is accepted.

## Workflow Contract

`CoverageGateTests` parses `.github/workflows/ci.yaml` and
`.github/workflows/release.yaml` with the repository's existing transitive
YamlDotNet dependency. Tests navigate mappings and sequences rather than
searching the whole file for strings.

The structural contract proves:

- `coverage-projects.needs` is `coverage-plan`.
- `coverage-summary.needs` contains `coverage-plan` and `coverage-projects`.
- release packaging jobs depend on `coverage-summary`.
- the project job contains the `run-one.sh` command and project artifact.
- the summary job contains `find-results.sh`, `summarize.py`, and the final
  `core-coverage` artifact with both output and policy paths.

Existing narrow string checks unrelated to workflow structure remain unchanged.

## Git Delivery

Before rewriting history, create a backup branch at the original HEAD and stash
all staged, unstaged, and untracked state. Split `3c3887ad` into one Coverage
commit and one post-change workflow commit, replay the two existing design
commits, restore the stash with its index, and commit the current Coverage
closure using an explicit path allowlist. Unrelated staged and untracked files
must retain their original state.

The final audit compares the rewritten history and working tree against the
allowlist, confirms all six helpers are tracked, and confirms no backup tree was
modified.

## Verification

- Output-path self-test with accepted and rejected temporary paths.
- Coverage-specific Architecture tests, then all Architecture tests.
- Coverage policy, project-list, matrix, and summary self-tests.
- Full Core Coverage run using `/private/tmp/bukit-coverage-final-*`.
- `ci-fast`, security regression, post-change targeted gate, shell/Python syntax,
  whitespace checks, and final Git scope audit.

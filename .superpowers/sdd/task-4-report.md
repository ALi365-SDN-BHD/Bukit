# AD-01B4 Report: Aggregate closure and documentation

## Scope completed

- Added the formal Chinese AD-01 closure ledger:
  `docs/analysis/bukit-core-ad01-config-decoupling-final-closure-2026-07-24.zh-CN.md`.
- Updated active architecture guidance with the config ownership and
  `BuildContext.Data` boundary.
- Updated active public API governance with the exact 2.0 CLR delta and
  migration link.
- The documentation change did not modify tests, schemas, plugin protocols,
  gates, Labs, external plugin implementations, historical audit snapshots,
  G-04 historical ledgers, or protected backup/reference directories. Two
  narrow production style fixes required by aggregate ratchets are recorded
  separately below.

Parent base:
`b14edc7d16e8ecdcfaf3a27712f86fe74fa0669b`.

Code terminal:
`1d3f7b9f1db23ba2dcd67a75fd0cbcaf35f2374f`.

## Evidence inventory

The closure ledger records:

- the original AD-01 root cause;
- B1 `5a7a30ad`;
- B2 `042c3203` plus review fix `73d14e34`;
- B3 `42b0b0c9` plus review fixes `171fc428` and `466f4a26`;
- aggregate ratchet remediations `4a53a744` and `1d3f7b9f`;
- RED/GREEN, focused-gate, and independent-review closure for each phase;
- the final dependency graph and explicit plugin-session ownership;
- exact public API migration and compatibility examples;
- unchanged schema, protocol, output, asset, security, and AOT-design
  boundaries;
- known consumer evidence without claiming external-consumer absence;
- atomic rollback groups and residual risks.

## Fresh focused verification

All commands ran sequentially outside the sandbox with `NOTION_TOKEN` removed:

```sh
env -u NOTION_TOKEN dotnet test \
  tests/Bukit.Engine.Abstractions.Tests/Bukit.Engine.Abstractions.Tests.csproj \
  --no-restore --nologo --verbosity quiet
```

Result: exit 0; 61 passed, 0 failed, 0 skipped.

```sh
env -u NOTION_TOKEN dotnet test \
  tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj \
  --no-restore --nologo --verbosity quiet
```

Result: exit 0; 1628 passed, 0 failed, 0 skipped.

```sh
env -u NOTION_TOKEN dotnet test \
  tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj \
  --no-restore --nologo --verbosity quiet
```

Result: exit 0; 618 passed, 0 failed, 0 skipped.

```sh
env -u NOTION_TOKEN dotnet test \
  tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj \
  --no-restore --nologo --verbosity quiet
```

Result: exit 0; 264 passed, 0 failed, 0 skipped.

## Aggregate and authorized replacement chain

Before the run, task reports and SDD records were searched for the fixed base.
B1, B2, and B3 explicitly recorded that no parent aggregate had run; no prior
invocation against this base was found.

The path list was the sorted, de-duplicated union of:

- `git diff --name-only
  b14edc7d16e8ecdcfaf3a27712f86fe74fa0669b...HEAD`;
- the four B4 tracked/new paths.

Ignored/untracked briefs and review packages were excluded.

Command:

```sh
env -u NOTION_TOKEN bash scripts/checks/post-change-targeted.sh \
  --base b14edc7d16e8ecdcfaf3a27712f86fe74fa0669b \
  -- "${changed_paths[@]}"
```

Final authorized replacement result: exit 0. It reached completion with all
diff, documentation, contract, self-test, public API drift, Release owner-test,
and Architecture stages passing.

That exit-0 result was the end of the following complete, authorized evidence
chain:

1. The original aggregate passed CLI 618/618, Abstractions 61/61,
   Engine 1628/1628, and Architecture 264/264, then exited 1 at the
   code-analysis ratchet:
   - `IDE0301`: 181, baseline 180;
   - `IDE0305`: 135, baseline 134.
2. Commit `4a53a744` made only the two narrow collection-style corrections.
   Focused Engine passed 1628/1628; raw style returned to
   `IDE0301=180` and `IDE0305=134`.
3. The first replacement aggregate was explicitly authorized by the user. It
   exited 1 on the next real ratchet violation: `CA1859=89`, baseline 88.
4. Commit `1d3f7b9f` made one private return-type correction. Focused Engine
   passed 1628/1628; raw counts were `IDE0301=180`, `IDE0305=134`, and
   `CA1859=88`; independent narrow review was COMPLIANT.
5. The user explicitly authorized a second replacement aggregate. It ran
   outside the sandbox with `NOTION_TOKEN` removed, against the same base and
   76 frozen paths, and exited 0:
   - CLI 618/618;
   - Engine.Abstractions 61/61;
   - Engine 1628/1628;
   - Architecture 264/264;
   - style 584/593;
   - analyzers 323/326;
   - public API drift, documentation/contracts, brainstorm server self-test,
     YAML static context, and all remaining reached stages passed.

Both failed aggregates remain part of the evidence. No third replacement
aggregate and no standalone `ci-fast` command ran; each aggregate reached the
internal fast contract gate only through `post-change-targeted.sh`. No full,
release, `test-all`, `smoke-all`, or Native AOT command ran.

## Static self-check

- Active Markdown links and repository-relative paths: checked.
- Placeholders and public absolute paths: none.
- Commit and test evidence: matched against reports and `git log`.
- Public baseline: 14 assemblies / 443 types / exact three AD-01 semantic
  changes.
- `git diff --check`: exit 0.

## Commit

- Subject: `docs(core): close ad01 config decoupling`
- The final commit hash is returned in the handoff because a file within a
  commit cannot contain that commit's stable hash.

## Residuals

- Private, unindexed, undisclosed, and binary-only direct CLR consumers remain
  unknown.
- Public `BuildContext.Data` remains caller-writable; the no-Config invariant
  applies to current Core built-in production writers.
- Native AOT proof was not authorized or run; only the static, reflection-free
  registration design remained covered.
- Independent whole-branch read-only review is dispatched by the parent
  controller after this commit.

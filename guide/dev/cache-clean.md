# Cache And Clean

The output selected by `clean` depends on the invocation:

| Invocation | Output target |
|---|---|
| `bukit clean` | `dist` below the current working directory; does not load `site.yaml`. |
| `bukit clean --dir <path>` | The selected path below the current working directory. |
| `bukit clean --config <file>` or `bukit clean --site <name>` | The resolved configuration's output directory; `--dir` is ignored when either selector is present. |

After output cleanup succeeds, all three forms also delete `.cache` and `.bukit`
below the selected root (the current directory or resolved config root). They
do not discover custom cache paths. To clean a configured `public` output, use
`bukit clean --config site.yaml`, not the bare command.

During a build, `BuildPlanner` applies `build.clean` to staged output after the
formal target has passed the same safety checks. Explicit `clean` deletes its
targets directly; it is not a build transaction.

## Safety Rules

`OutputDirectoryCleaner` is shared by `CleanCommand`, `BuildPlanner`, and build
recovery. It refuses to clean:

- The site root.
- The user home directory.
- Filesystem root.
- Paths outside the site root.
- `.git` or any descendant containing a `.git` segment.
- Targets reached through a symlink/reparse-point segment below the site root.
- Non-empty output directories without `.bukit-output-marker`.

The marker is necessary for a non-empty directory but does not bypass the other
checks. Rejected clean requests preserve the target and return the config/setup
error path rather than continuing with a success message.

## Recovery

`BuildTransaction` keeps formal output and managed caches unchanged when a
build fails before commit. Fix the failure and retry; ordinary render failure
does not require deleting the last successful output. `BuildRecoveryTracker`
writes started/completed state in staging. Legacy incomplete formal output can
still trigger marker-protected recovery cleanup on a non-clean build.

If commit fails, Core attempts reverse restoration. Preserve any backup paths
reported after failed restoration until manual recovery is complete. A warning
about backup cleanup after commit means the new output was committed. Do not
treat it as an instruction to roll back or delete the new output. See
[Build transactions](cli.md#build-transactions) for the full recovery limits.

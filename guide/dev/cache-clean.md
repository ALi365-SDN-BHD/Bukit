# Cache And Clean

`clean` removes the configured output directory plus `.cache` and `.bukit`.
`BuildPlanner` also cleans before a build when `build.clean` is true.

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

`BuildRecoveryTracker` marks output as started and completed. If a previous
build was incomplete and `build.clean` is false, the next build can auto-clean
to recover a safe output state.

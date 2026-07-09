# Cache And Clean

`clean` removes the configured output directory plus `.cache` and `.bukit`.
`BuildPlanner` also cleans before a build when `build.clean` is true.

## Safety Rules

`BuildPlanner` refuses to clean:

- The site root.
- The user home directory.
- Filesystem root.
- `.git`.
- Non-empty output directories without `.bukit-output-marker`.

`CleanCommand` requires explicit directories to remain inside the current
directory when no config is provided.

## Recovery

`BuildRecoveryTracker` marks output as started and completed. If a previous
build was incomplete and `build.clean` is false, the next build can auto-clean
to recover a safe output state.

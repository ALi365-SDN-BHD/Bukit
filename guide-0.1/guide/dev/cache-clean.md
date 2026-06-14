# Cache and Clean (cache-dir / .cache / clean)

The project's "cache" primarily serves incremental builds (skipping unchanged pages), while "clean" clears the output and cache directories to prevent stale state in local/CI environments.

Related docs:
- [Incremental Build](./incremental-build.md)
- [CLI Command Reference](./cli.md)

## Cache Directory Definition

### Default: `.cache/`

The build engine defaults to `<rootDir>/.cache/` for storing incremental build manifests:
- Single language: `build-manifest.json`
- Multi-language: `build-manifest.<lang>.json`

`doctor` also checks that the manifest JSON in this directory is parseable (see [doctor.zh-CN.md](./doctor.zh-CN.md)).

### Override: `--cache-dir <dir>`

You can override the cache directory via CLI (useful for CI isolation or parallel builds):
- The manifest will be written to the specified directory
- Multi-language manifests are still separated by `<lang>` suffix

## What `clean` Cleans

`bukit clean` behavior:

1. Deletes the output directory (specified by `--dir`, or resolved from `--config/--site` → `build.output`)
2. Deletes `<rootDir>/.cache/` (incremental manifest cache)
3. Compatibility cleanup: deletes `<rootDir>/.bukit/` (legacy cache directory)

Note: `clean` will never delete your content directories (`content/`, `data/`), theme directories (`layouts/themes`), or any config files.

## Clean Marker Protection

Since v3.x, `build --clean` and `build.clean: true` require a `.bukit-output-marker` file to exist in the output directory before deleting it. This marker is written on every successful build and serves as a safety guard:

- Directories without the marker are **never cleaned** — this prevents accidental deletion of non-Bukit directories.
- Bukit also refuses to clean the project root, home directory, filesystem root, or `.git` directories.

If `clean` is refused:
- If the directory was created by Bukit: run a full build first (it writes the marker), then clean.
- If the directory is not a Bukit output: delete it manually or choose a different output directory.

## When to Clean

- "I changed templates/routes/content but the output looks unchanged": check if incremental is on first, then consider clean
- "Output is wrong after switching languages/default language": clean is recommended to avoid cross-variant artifact conflicts
- CI wants fully reproducible builds: use `build --clean`; if cache interference remains, use `clean`

## FAQ

1. Difference between `build --clean` and `clean`?
   - `build --clean`: cleans only the output directory (`build.output`), then builds
   - `clean`: cleans output directory + cache directory (`.cache`, etc.)

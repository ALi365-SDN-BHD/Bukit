# Cache and Clean

Cache supports incremental rendering. Clean removes generated state while
protecting source directories and non-Bukit paths.

Source anchors:

- `src/Bukit.Cli/Commands/CleanCommand.cs`
- `src/Bukit.Engine/BuildPipeline.cs`
- `src/Bukit.Engine/BuildOutputCleaner.cs`

## Cache Directory

Default: `.cache/`

Used for:

- incremental build manifests;
- provider caches such as Notion page index data;
- runtime state that can be regenerated.

Override with:

```bash
bukit build --cache-dir .cache-ci
```

## Clean Command

```bash
bukit clean
bukit clean --dir dist
bukit clean --config site.yaml
```

`clean` removes:

- the resolved output directory;
- `.cache/`;
- `.bukit/` compatibility state.

It does not remove content, data, themes, config, or source files.

## Build-Time Clean

```bash
bukit build --clean
bukit build --no-clean
```

`build.clean` controls default build-time cleaning, and CLI flags override it.

## Safety Rails

- Bukit refuses to clean project root, home, filesystem root, and `.git`.
- Output cleaning uses a marker file to avoid deleting non-Bukit directories.
- Interrupted builds are detected and recovered on the next build.

## CI Guidance

Use `build --clean` for reproducible release output. Use an isolated
`--cache-dir` when parallel jobs build the same workspace.


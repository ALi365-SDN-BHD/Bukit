# Import Plugin Package Smoke Contract

This contract covers PR-Import-011 / M13 only: package build and
cross-platform smoke for `Bukit.Plugin.Import`.

## Package Build

Run:

```bash
bash scripts/build/import-plugin-package.sh TestResults/import-plugin-package Release
```

The build script publishes the Import process plugin for:

- `win-x64`
- `linux-x64`
- `osx-arm64`

The output layout is:

```text
TestResults/import-plugin-package/
  .bukit/plugins.yaml
  plugins/import/plugin.yaml
  plugins/import/bin/win-x64/bukit-plugin-import.exe
  plugins/import/bin/linux-x64/bukit-plugin-import
  plugins/import/bin/osx-arm64/bukit-plugin-import
```

The source fixture manifest keeps placeholder `sha256` values. The package
build output rewrites every platform `sha256` to the actual executable hash.
Generated executables are release artifacts and must not be committed.

## Smoke

Run:

```bash
bash scripts/smoke/import-plugin-package.sh TestResults/import-plugin-package Release
```

The smoke script validates:

- `plugin.yaml` declares `kind=process`, `protocol=bukit-plugin-v1`, and
  `distribution=self-contained`.
- `plugin.yaml` declares `win-x64`, `linux-x64`, and `osx-arm64`.
- Every platform entry points inside `plugins/import/bin/<rid>/`.
- Every executable exists and matches the manifest `sha256`.
- `.bukit/plugins.yaml` does not contain `entry`.
- `bukit plugin validate-config` passes.
- `bukit plugin validate-manifest plugins/import` passes.
- The host RID package can execute:
  - `bukit import seed ./package-smoke-seed --output ./content --force`
  - `bukit import html-demo ./package-smoke-demo --theme package-smoke --dry-run`

The smoke executes only the host RID binary. Non-host RIDs are structurally
validated by path, executable presence, and hash.

## Boundary

The package build does not restore dynamic DLL plugins, `site.externalPlugins`,
or `.bukit` executable entries. The Import plugin remains a process plugin and
does not reference Labs, `Bukit.Cli`, or `Bukit.PluginHost`.

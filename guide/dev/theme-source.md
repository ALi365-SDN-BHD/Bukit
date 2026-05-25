# Git Theme Source

Bukit supports pulling themes from Git repositories, enabling theme distribution and version management. No centralized registry needed — just declare a Git URL in `site.yaml`.

Implementation:
- `src/Bukit.Engine/ThemeSourceManager.cs`
- `src/Bukit.Config/AppConfig.cs` (ThemeConfig.Source)
- `src/Bukit.Engine/SiteEngine.cs` (BuildVariantAsync)

## `site.yaml` Configuration

```yaml
theme:
  source: "https://github.com/user/bukit-theme.git@v1.2.0"
  name: my-custom    # optional: subdirectory name within the repo
```

| Field | Required | Description |
|------|------|------|
| `theme.source` | Yes | Git repo URL + optional version tag (`@v1.0.0`) |
| `theme.name` | No | Theme subdirectory within the repo. If not specified, uses repo root |

## Version Pinning

Versions are specified via the `@` suffix in the URL:

```
https://github.com/user/theme.git@v1.0.0   # Git tag
https://github.com/user/theme.git@abc1234   # commit hash
https://github.com/user/theme.git           # default main/master branch
```

When no version is specified, the default branch is used.

## Caching and Reproducibility

- **First build**: `git clone` to `.cache/themes/{repo-name}/`
- **Subsequent builds**: cached themes are **not** automatically updated (`git pull` is not called). The previously-checked-out commit is reused — this ensures reproducible builds.
- When `@ref` (e.g., `@v1.0.0`) is specified, Bukit checks out that exact tag/branch and records the resolved commit.
- Missing version tags cause immediate build failure (no silent fallback to other branches).

## Theme Lock File

After a successful checkout, Bukit writes `bukit-theme.lock.json` to the local cache directory:

```json
{
  "themes": [
    {
      "source": "https://github.com/user/theme.git",
      "ref": "v1.0.0",
      "commit": "abc123def456..."
    }
  ]
}
```

On subsequent builds, Bukit validates that the checked-out commit matches the recorded lock file commit. If they differ, the build fails with a clear error — this prevents unexpected remote theme changes.

To update a locked theme: delete the cache directory or the lock file and rebuild.

## Priority with Local Themes

When both `theme.source` and local `themes/` directory are configured:

- `theme.source` takes priority — Git pull is attempted first
- If Git pull fails (network error, invalid repo), falls back to local `themes/` directory
- `theme.name` only locates the subdirectory within the repo and does not affect local priority

## Environment Requirements

- Build environment must have `git` CLI installed
- Repository must be publicly accessible (or SSH key configured)
- Clone/checkout timeout: 120 seconds

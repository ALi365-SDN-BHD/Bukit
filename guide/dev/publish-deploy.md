# Publish and Deploy

Core has two separate concerns:

1. publish-quality gates over generated static output;
2. GitHub Pages deployment through `bukit deploy`.

Source anchors:

- `src/Bukit.Cli/Commands/SeoCommand.cs`
- `src/Bukit.Cli/Commands/GeoCommand.cs`
- `src/Bukit.Cli/Commands/PublishCommand.cs`
- `src/Bukit.Cli/Commands/DeployCommand.cs`
- `src/Bukit.Cli/Deploy/GitHubPagesDeployProvider.cs`
- `src/Bukit.Config/DeployConfig.cs`

## Publish Gates

```bash
bukit build
bukit seo audit --dir dist
bukit geo audit --dir dist
bukit publish audit --dir dist
```

Use `--strict` when warnings should fail CI. Use `diff` subcommands to compare
current reports with a baseline.

## Deploy Config

```yaml
deploy:
  provider: github-pages
  branch: gh-pages
  message: "Deploy site"
  cname: example.com
  keepHistory: true
```

`deploy.provider` must be `github-pages` when `deploy` exists.

## Deploy Commands

```bash
bukit deploy --dry-run
bukit deploy --branch gh-pages --message "Deploy site"
bukit deploy --skip-build --output dist
```

By default, `deploy` runs a build first. `--skip-build` deploys existing output
after config validation.

## URL Overrides

Use `--base-url` and `--site-url` to match the destination:

```bash
bukit deploy --base-url /repo --site-url https://owner.github.io/repo
```

## Repository Workflow Note

The Bukit repository's own release workflow publishes Bukit CLI binaries. It is
not a ready-to-copy workflow for a user's static site. Site deployment examples
should be written as site-owned GitHub Pages workflows.


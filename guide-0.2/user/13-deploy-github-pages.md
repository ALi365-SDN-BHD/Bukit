# Deploy to GitHub Pages

Bukit Core 1.0 deploys to GitHub Pages through `deploy.provider: github-pages` and the `bukit deploy` command.

## Config

```yaml
site:
  url: https://example.github.io
  baseUrl: /my-site/
deploy:
  provider: github-pages
  branch: gh-pages
  message: "Deploy site"
  keepHistory: true
```

For a custom domain:

```yaml
deploy:
  provider: github-pages
  cname: example.com
```

## Verify Before Deploy

```bash
bukit config check
bukit doctor
bukit build
bukit publish audit --dir dist
bukit deploy --dry-run
```

## Deploy

```bash
bukit deploy
```

Use `--skip-build` only when `dist/` was produced by the same config and commit.

## GitHub Actions Note

The Bukit repository release workflow is for publishing Bukit binaries. Do not copy that release workflow as a user-site Pages workflow. A user-site workflow should install or download Bukit, run the verification chain, and publish the generated `dist/` directory.

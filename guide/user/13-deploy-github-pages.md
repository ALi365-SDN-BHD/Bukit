# 13 Deploy GitHub Pages

Core deployment is implemented by `GitHubPagesDeployProvider` and supports only
`deploy.provider: github-pages`.

## Config

```yaml
site:
  url: https://example.github.io
  baseUrl: /my-site/

deploy:
  provider: github-pages
  branch: gh-pages
  message: bukit deploy
  keepHistory: false
```

Use `cname` when GitHub Pages should publish a custom domain.

## Dry Run

```bash
bukit config check
bukit deploy --dry-run
```

Dry run validates config and prints the plan without writing to the remote.

## Deploy

```bash
bukit deploy --ci --branch gh-pages --message "publish site"
```

Useful options:

| Option | Use |
|---|---|
| `--skip-build` | Deploy the existing output directory. |
| `--output` | Override `build.output` for this invocation. |
| `--base-url`, `--site-url` | Override generated URLs for deployment. |
| `--force` | Allow forced branch update when explicitly intended. |

Do not copy this repository's release workflow for a site. It releases Bukit
binaries. Use a site-specific GitHub Pages workflow.

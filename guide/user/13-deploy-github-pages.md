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

## Publish Privacy Boundary

The GitHub Pages provider copies only public build output into its temporary
branch checkout. It excludes these internal artifacts:

- `.bukit/`
- `.bukit-build-state.json`
- `.bukit-output-marker`
- nested `.git/` directories

Other public dotfiles and directories, including `.well-known/`, remain
deployable.

Before `git add` or `git push`, the provider validates the staged public tree.
It rejects generated `source: notion` or `sourceKey: notion` markers and, when
the internal publish audit report supplies the source identity, exact Notion
page identifiers. The failure names the affected relative output path without
echoing the identifier.

Prefer `bukit deploy --ci` so the build-time `publicOutputPrivacy` security
check is strict as well. When using `--skip-build`, only deploy output produced
by the current Bukit version. The staging validator still runs and fails closed when
`.bukit/publish-audit-report.json` is missing or invalid, because deployment
cannot otherwise distinguish a Notion identifier from an unrelated UUID.

Do not copy this repository's release workflow for a site. It releases Bukit
binaries. Use a site-specific GitHub Pages workflow.

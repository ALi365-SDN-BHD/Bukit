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

The deployment repository always comes from the project selected by `--config`
(or `--site`), not from the current working directory. Bukit reads
`remote.origin.url` from the project's worktree scope first and falls back to
its local scope only when that key is absent. The first raw origin value wins: an
empty, invalid, or non-direct GitHub origin fails instead of using a later
value, another repository, or a global/system origin. `--output` changes only
where artifacts are read; it does not change repository identity.

Origin must point directly at GitHub, for example
`https://github.com/owner/repo.git` or `git@github.com:owner/repo.git`. Aliases
that become GitHub URLs only through global Git URL rewriting are not accepted.

Bukit deploys through a temporary checkout with a private remote, so a global or
system `origin` with the same name cannot redirect the deployment. Before every
branch query, fetch, and push it checks the effective configuration in that
checkout. Any `insteadOf`/`pushInsteadOf` rule that would rewrite the canonical
GitHub URL is rejected before any transport starts, including rules activated
by `includeIf hasconfig:remote.*.url:` or `includeIf gitdir:` after the
repository and remote are created. Remove the matching rewrite rule; unrelated
rules and normal credential, proxy, and SSH settings remain usable.

Inherited overrides such as `GIT_DIR`, `GIT_WORK_TREE`, `GIT_COMMON_DIR`,
`GIT_INDEX_FILE`, object/alternate directories, `GIT_CONFIG`,
`GIT_CONFIG_PARAMETERS`, and `GIT_CONFIG_COUNT`/`GIT_CONFIG_KEY_*`/`VALUE_*`
are removed from deployment subprocesses. This is an intentional tightening:
deployment cannot be redirected to another repository or index through the
environment. Normal repository subdirectories and linked worktrees still work
through their own Git metadata. Parent process environment and global/system
credential configuration are not modified.

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
page identifiers. JSON string values and property names are checked after
decoding, so `\u`-escaped identifiers are detected. The failure names the
affected relative output path without echoing the identifier.

The same privacy boundary also fails closed when a JSON file cannot be checked:
malformed JSON, JSON nested beyond the parser depth limit, or strings with
unpaired Unicode surrogates are reported as "unable to complete" rather than
claimed clean or reported as a confirmed identifier leak. Fix the JSON or invalid
Unicode and rebuild, then deploy again. A `warn` build may complete and publish
output with a failed `publicOutputPrivacy` report, but deployment still refuses
that output independently.

Prefer `bukit deploy --ci` so the build-time `publicOutputPrivacy` security
check is strict as well. When using `--skip-build`, only deploy output produced
by the current Bukit version. The staging validator still runs and fails closed when
`.bukit/publish-audit-report.json` is missing or invalid, because deployment
cannot otherwise distinguish a Notion identifier from an unrelated UUID.

Do not copy this repository's release workflow for a site. It releases Bukit
binaries. Use a site-specific GitHub Pages workflow.

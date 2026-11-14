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

By default the deployment repository is the project's `origin`. The project is
selected by `--config` (or `--site`), not by the current working directory. Use
`--repo owner/name` or `deploy.repo` to deploy to another GitHub repository; see
[Target repository](#target-repository). Bukit reads
`remote.origin.url` from the project's worktree scope first and falls back to
its local scope only when that key is absent. The first raw origin value wins: an
empty, invalid, or non-direct GitHub origin fails instead of using a later
value, another repository, or a global/system origin. `--output` changes only
where artifacts are read; it does not change repository identity.

When the target comes from origin, origin must point directly at GitHub, for example
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

### Target repository

Precedence: `--repo owner/name`, then `deploy.repo` in `site.yaml`, then the
project's `origin`. `deploy.repo` is committed with the site, so anyone who can
merge configuration changes can change the deployment target. The token's
repository scope is the real limit: use a fine-grained token that covers only
the target repository. When the target differs from `origin`, Bukit logs a
warning and deploys to the target. `--repo` is an explicit option; it is not read
from the environment.

An empty value is an error, never a silent fallback to `origin`: this covers
`--repo ""`, `repo: ""` in `site.yaml`, and an empty `BUKIT_DEPLOY__REPO`
(for example an undefined Actions variable). Remove the key to deploy to `origin`.

Every command reads `BUKIT_DEPLOY__*` variables, so an empty `BUKIT_DEPLOY__REPO`
also fails `bukit build`, `bukit lint` and `bukit doctor`. In CI, set it only on
the deploy step. Surrounding whitespace in `deploy.repo` is trimmed, as for `--repo`.

Useful options:

| Option | Use |
|---|---|
| `--repo <owner/name>` | Deploy to this GitHub repository instead of the project origin. |
| `--skip-build` | Deploy the existing output directory. |
| `--output` | Override `build.output` for this invocation. |
| `--base-url`, `--site-url` | Override generated URLs for deployment. |
| `--token-env <NAME>` | Read the GitHub token named `NAME` instead of `GITHUB_TOKEN`: from `--secrets-stdin` input first, then the environment. |
| `--secrets-stdin` | Read tokens from one JSON object on stdin, such as `{"GITHUB_TOKEN":"…"}`, instead of the environment. See the CLI reference. |
| `--force` | Allow forced branch update when explicitly intended. |

## Token

Bukit reads the GitHub token from an environment variable, or from standard
input with `--secrets-stdin`, never from `site.yaml` or a command-line value.
By default the name is `GITHUB_TOKEN`.
Use `--token-env NAME` when a run needs a different variable, for example when
one job deploys with a token scoped to another repository.

- The name starts with an upper-case letter or `_`, then uses only upper-case
  letters, digits, and underscores, up to 128 characters, such as
  `DOCS_DEPLOY_TOKEN`. Lower-case names are rejected on purpose, so a token
  pasted by mistake is refused and its value is not printed.
- Pass only the variable name. Never paste a token into the command: shell
  history and CI logs such as `set -x` output can keep it. If a token was pasted,
  revoke it and create a new one.
- The variable must hold a token with `Contents: Read and write` on the
  deployment repository. A missing variable fails before any remote Git operation,
  and the error names the variable without printing its value.
- In GitHub Actions, `GITHUB_TOKEN` is not set as an environment variable
  automatically. For `bukit deploy` in a workflow, add `permissions: contents: write`
  and `env: GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}`. This applies only to
  `bukit deploy`; `actions/deploy-pages` does not need a token push.
- The Actions `GITHUB_TOKEN` only works for the workflow's own repository. To
  deploy to another repository, store a fine-grained personal access token as a
  secret and pass its variable name with `--token-env`. A GitHub App
  installation token expires after about one hour, so do not store one as a
  secret: store the App ID and private key, create the token in the workflow,
  and pass it with `--token-env`.

- With `--secrets-stdin`, write `{"GITHUB_TOKEN":"…"}` (or the name given to
  `--token-env`) to Bukit's stdin. The stdin value wins over the environment
  variable. Bukit then passes the token to each authenticated `git` process in
  that process's environment (`BUKIT_GITHUB_TOKEN`, read by a temporary
  `GIT_ASKPASS` script), so other programs of the same user can read it while
  `git` runs.

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

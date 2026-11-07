---
name: bukit-deploy
description: Use for GitHub Pages deployment config, dry runs, and deploy command options.
---

# Bukit Deploy

Core supports `deploy.provider: github-pages`. Use `bukit deploy --dry-run`
first, then deploy with the target branch, message, and CI options.

For a token other than `GITHUB_TOKEN`, pass `--token-env NAME`. The value is the
variable name, never the token itself.

To deploy to another GitHub repository, pass `--repo owner/name` (or set `deploy.repo`).
Without either, the project `origin` is the target.

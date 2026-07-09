# Publish And Deploy

Publishing quality and deployment are separate.

## Publish Audit

`publish audit` reads `.bukit/publish-audit-report.json`, validates
representation coverage and issue severity, and can treat warnings as failures.
`publish diff` compares two reports and enforces budgets for new issues,
removed routes, and indexability drops.

## Deploy

`DeployCommand` validates config, optionally runs `build`, then calls
`GitHubPagesDeployProvider`. Supported provider is `github-pages`.

Deploy provider responsibilities:

- Validate branch names and CNAME.
- Prepare a temporary worktree or staging area.
- Copy output.
- Commit and push.
- Respect dry-run, skip-build, branch, message, CI, and force options.

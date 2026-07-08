# Labs: Webhook Trigger

Status: not Core 1.0.

The historical webhook command translated external events into GitHub
`repository_dispatch` events. It is not part of the Core command registry.

## Core Boundary

Core deploy is GitHub Pages through `bukit deploy`. Core docs should not claim
that the default CLI hosts webhook listeners.

## Historical Shape

Older drafts described a command shaped like:

```bash
bukit webhook --repo owner/repo --port 8787 --path /webhook/notion
```

Do not present this as Core 1.0 behavior.

## Labs Re-Entry Requirements

Webhook work needs:

- a Labs-owned host or command surface;
- explicit authentication and token handling;
- replay and signature strategy;
- network binding policy;
- GitHub API error handling;
- CI and deployment documentation separated from Core static-site deploy.


# Webhook (Triggers and Security Constraints)

The webhook subcommand converts external events (e.g., Notion webhooks) into GitHub `repository_dispatch` to trigger build workflows.

Implementation: `src/Bukit.Cli/Commands/WebhookCommand.cs`

## Basic Usage

```bash
dotnet run --project src/Bukit.Cli -c Release -- webhook --repo owner/repo --port 8787 --path /webhook/notion --event bukit_notion
```

Listens on: `http://<host>:<port><path>` (POST only)

## Parameters

| Parameter | Default | Description |
|---|---|---|
| `--host <host>` | `localhost` | Listen address |
| `--port <port>` | `8787` | Listen port |
| `--path <path>` | `/webhook/notion` | Request path |
| `--repo <owner/repo>` | - | GitHub repository (also via env) |
| `--event <event_type>` | `bukit_notion` | repository_dispatch event_type |

## Required Environment Variables

| Variable | Purpose |
|---|---|
| `BUKIT_WEBHOOK_TOKEN` | Inbound auth token (header `X-Sitegen-Token`) |
| `BUKIT_GITHUB_TOKEN` (or `GITHUB_TOKEN`) | GitHub API token |

## Security Constraints
- Must be POST; path exact match; `X-Sitegen-Token` must equal `BUKIT_WEBHOOK_TOKEN`
- 405 (non-POST), 404 (path mismatch), 401 (token mismatch)

## Trigger Behavior
Sends `POST https://api.github.com/repos/<owner/repo>/dispatches` with `{ event_type, client_payload }` and returns 202.

---
name: bukit-webhook
description: Use when using bukit to set up a webhook listener that authenticates incoming triggers and dispatches a GitHub repository_dispatch event, configuring Notion-to-GitHub webhook triggers, troubleshooting token verification or rate limiting, or understanding webhook security constraints

status: stable
since: "v3.0.0"
verified_by:
  - "src/Bukit.Cli/Commands/WebhookCommand.cs"
source_anchors:
  - "src/Bukit.Cli/Commands/WebhookCommand.cs"
guide_chapters:
  - "guide/user/14-troubleshooting.md"
---

# Webhook Server

## Overview

The `webhook` command starts an HTTP listener that receives webhook payloads (typically from Notion), verifies a shared token, and sends a GitHub `repository_dispatch` event. The actual build/deploy work should happen in the GitHub Actions workflow that handles that dispatch event.

**REQUIRED BACKGROUND:** Notion integration setup — see bukit-notion for Notion API configuration.
**REQUIRED SUB-SKILL:** CLI commands reference bukit-cli-reference.

## Multilingual Triggers / Pencetus Berbilang Bahasa

| Language | Trigger Phrases |
|----------|----------------|
| 中文 | "Webhook 自动部署"、"Notion 更新触发构建"、"bukit webhook"、"token 验证" |
| English | "webhook auto deploy", "Notion update trigger build", "bukit webhook", "token verification" |
| Bahasa Melayu | "webhook auto deploy", "Notion kemas kini cetus binaan", "bukit webhook", "pengesahan token" |

## Prerequisites

| Requirement | Environment Variable | Description |
|------|------|------|
| Webhook token | `BUKIT_WEBHOOK_TOKEN` | Shared secret expected in the `X-Sitegen-Token` request header |
| GitHub token | `BUKIT_GITHUB_TOKEN` or `GITHUB_TOKEN` | GitHub PAT with `repo` scope for repository dispatch |
| GitHub repo | `BUKIT_GITHUB_REPO` or `--repo` | Repository in `owner/repo` format |

## Usage

### Basic

```bash
export BUKIT_WEBHOOK_TOKEN="your-secret-token"
export BUKIT_GITHUB_TOKEN="ghp_xxxx"
export BUKIT_GITHUB_REPO="user/my-site"

bukit webhook
```

Starts a server at `http://localhost:8787/webhook/notion`.

### Custom Host, Port, and Path

```bash
bukit webhook --host 0.0.0.0 --port 9000 --path /hooks/deploy
```

### Custom Event Type

```bash
bukit webhook --event my_custom_event
```

The `--event` parameter sets the GitHub `repository_dispatch` event type sent to the target repository.

## Command Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `--host <addr>` | string | `localhost` | Host address to bind |
| `--port <port>` | int | `8787` | Port to listen on |
| `--path <path>` | string | `/webhook/notion` | URL path for incoming webhooks |
| `--repo <owner/repo>` | string | from env | GitHub repository (overrides `BUKIT_GITHUB_REPO`) |
| `--event <type>` | string | `bukit_notion` | GitHub `repository_dispatch` event type |

## Security

### Token Verification

The webhook server verifies incoming requests using a shared token. The sender must include:

- `X-Sitegen-Token`: the exact value of `BUKIT_WEBHOOK_TOKEN`

Mismatched or missing tokens receive `401 Unauthorized`.

### Rate Limiting

- **Max 10 requests** per **1-minute window**
- Exceeding the limit returns `429 Too Many Requests`
- Rate limit resets after the window expires

### IP Allowlisting

For production deployments, place the webhook behind a reverse proxy (nginx, Caddy) and rely on TLS, shared token verification, reverse proxy rate limiting, and network-level access controls.

## Webhook Payload Flow

```
1. Notion page published/updated
   ↓
2. Notion sends webhook to http://<host>:<port>/<path>
   ↓
3. Bukit verifies the `X-Sitegen-Token` shared token
   ↓
4. Bukit checks rate limit (10 req/min)
   ↓
5. Bukit sends GitHub repository_dispatch with `event_type`
   ↓
6. GitHub Actions handles build/deploy for that event
   ↓
7. Response: 202 Accepted or error status
```

## Dispatch Behavior

The webhook does not build locally and does not push site output itself. On each valid request it:

1. **Authenticates**: Compares `X-Sitegen-Token` with `BUKIT_WEBHOOK_TOKEN`.
2. **Rate limits**: Allows up to 10 requests per minute.
3. **Dispatches**: Uses `BUKIT_GITHUB_TOKEN` or `GITHUB_TOKEN` to call GitHub repository dispatch for `--repo`.

The server continues running after each request — it handles multiple triggers without restarting.

## Error Handling

| HTTP Status | Meaning |
|------|------|
| `202 Accepted` | Dispatch accepted |
| `401 Unauthorized` | Invalid or missing shared token |
| `429 Too Many Requests` | Rate limit exceeded |
| `500 Internal Server Error` | GitHub dispatch failed or another server error occurred |

## Common Issues

| Issue | Cause | Fix |
|------|------|------|
| `Missing env: BUKIT_WEBHOOK_TOKEN` | Token not set | Export the environment variable |
| `Missing --repo` or `BUKIT_GITHUB_REPO` | Repo not configured | Set `BUKIT_GITHUB_REPO=user/repo` or use `--repo` |
| `401 Unauthorized` | Shared token mismatch | Verify the sender sends `X-Sitegen-Token` with the configured token |
| `429 Too Many Requests` | Rate limit hit | Wait for the window to reset, or increase rate limit in code |
| GitHub dispatch fails | Invalid GitHub token or repo access | Check `BUKIT_GITHUB_TOKEN` has `repo` scope and `--repo` is correct |

## Production Deployment

For production use:

1. **Reverse proxy**: Place behind nginx/Caddy with TLS
2. **IP restriction**: Restrict inbound traffic to known webhook source IPs if stable at the firewall/reverse-proxy level
3. **Process manager**: Use systemd or supervisord to keep the webhook running
4. **Logging**: Redirect stderr to a log file for debugging
5. **Health check**: The webhook path responds to all valid requests — no separate health endpoint

### Example systemd Unit

```ini
[Unit]
Description=Bukit Webhook Server
After=network.target

[Service]
Type=simple
User=bukit
WorkingDirectory=/opt/bukit-site
Environment=BUKIT_WEBHOOK_TOKEN=xxx
Environment=BUKIT_GITHUB_TOKEN=xxx
Environment=BUKIT_GITHUB_REPO=user/repo
ExecStart=/usr/local/bin/bukit webhook --host 127.0.0.1 --port 8787
Restart=always
RestartSec=5

[Install]
WantedBy=multi-user.target
```

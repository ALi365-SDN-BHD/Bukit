---
name: bukit-webhook
description: Use when using bukit to set up a webhook server for automated builds, configuring Notion-to-GitHub webhook triggers, troubleshooting webhook payload verification or rate limiting, or understanding webhook security constraints (HMAC signature, IP allowlisting)
---

# Bukit Webhook Server

## Overview

Bukit's `webhook` command starts an HTTP listener that receives webhook payloads (typically from Notion), triggers `bukit build`, and pushes the output to GitHub. Designed for automated content deployment: update Notion → webhook triggers → build → deploy.

**REQUIRED BACKGROUND:** Notion integration setup — see bukit-notion for Notion API configuration.
**REQUIRED SUB-SKILL:** CLI commands reference bukit-cli-reference.

## Multilingual Triggers / Pencetus Berbilang Bahasa

| Language | Trigger Phrases |
|----------|----------------|
| 中文 | "Webhook 自动部署"、"Notion 更新触发构建"、"bukit webhook"、"HMAC 签名验证" |
| English | "webhook auto deploy", "Notion update trigger build", "bukit webhook", "HMAC signature verification" |
| Bahasa Melayu | "webhook auto deploy", "Notion kemas kini cetus binaan", "bukit webhook", "pengesahan tandatangan HMAC" |

## Prerequisites

| Requirement | Environment Variable | Description |
|------|------|------|
| Webhook token | `BUKIT_WEBHOOK_TOKEN` | Secret token for HMAC payload verification |
| GitHub token | `BUKIT_GITHUB_TOKEN` or `GITHUB_TOKEN` | GitHub PAT with `repo` scope for pushing |
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

The `--event` parameter sets the expected `x-bukit-event` header value. Only requests with this header value are processed.

## Command Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `--host <addr>` | string | `localhost` | Host address to bind |
| `--port <port>` | int | `8787` | Port to listen on |
| `--path <path>` | string | `/webhook/notion` | URL path for incoming webhooks |
| `--repo <owner/repo>` | string | from env | GitHub repository (overrides `BUKIT_GITHUB_REPO`) |
| `--event <type>` | string | `bukit_notion` | Expected event type in `x-bukit-event` header |

## Security

### HMAC Signature Verification

The webhook server verifies incoming payloads using HMAC-SHA256. The sender must include:

- `x-bukit-signature-256`: `sha256=<hex-encoded HMAC>`

The server computes `HMAC-SHA256(payload, BUKIT_WEBHOOK_TOKEN)` and compares. Mismatched signatures receive `401 Unauthorized`.

### Rate Limiting

- **Max 10 requests** per **1-minute window**
- Exceeding the limit returns `429 Too Many Requests`
- Rate limit resets after the window expires

### IP Allowlisting

For production deployments, place the webhook behind a reverse proxy (nginx, Caddy) and restrict inbound IPs to Notion's webhook IP ranges.

## Webhook Payload Flow

```
1. Notion page published/updated
   ↓
2. Notion sends webhook to http://<host>:<port>/<path>
   ↓
3. Bukit verifies HMAC signature (x-bukit-signature-256)
   ↓
4. Bukit checks rate limit (10 req/min)
   ↓
5. Bukit runs: bukit build --ci
   ↓
6. Bukit runs: git push to gh-pages branch
   ↓
7. Response: 200 OK (success) or error details
```

## Build and Deploy Behavior

The webhook performs a full build + deploy cycle on each valid request:

1. **Build**: `bukit build --ci` (CI mode reduces log verbosity)
2. **Deploy**: Uses `BUKIT_GITHUB_TOKEN` to push the output directory to the `gh-pages` branch
3. **Commit message**: Generated from the webhook payload, including Notion page info when available

The server continues running after each request — it handles multiple triggers without restarting.

## Error Handling

| HTTP Status | Meaning |
|------|------|
| `200 OK` | Build + deploy succeeded |
| `401 Unauthorized` | Invalid or missing HMAC signature |
| `429 Too Many Requests` | Rate limit exceeded |
| `500 Internal Server Error` | Build or deploy failed |

## Common Issues

| Issue | Cause | Fix |
|------|------|------|
| `Missing env: BUKIT_WEBHOOK_TOKEN` | Token not set | Export the environment variable |
| `Missing --repo` or `BUKIT_GITHUB_REPO` | Repo not configured | Set `BUKIT_GITHUB_REPO=user/repo` or use `--repo` |
| `401 Unauthorized` | HMAC signature mismatch | Verify the webhook sender uses the correct token and HMAC algorithm |
| `429 Too Many Requests` | Rate limit hit | Wait for the window to reset, or increase rate limit in code |
| Build fails silently | Build errors in CI mode | Run `bukit build` locally to see full errors |
| Deploy fails | Invalid GitHub token or repo access | Check `BUKIT_GITHUB_TOKEN` has `repo` scope |

## Production Deployment

For production use:

1. **Reverse proxy**: Place behind nginx/Caddy with TLS
2. **IP restriction**: Allow only Notion webhook IPs at the firewall/reverse-proxy level
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

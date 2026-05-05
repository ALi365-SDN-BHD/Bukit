# Security Policy

## Supported Versions

| Version | Supported          |
|---------|--------------------|
| 1.0.x   | :white_check_mark: |

## Reporting a Vulnerability

If you discover a security vulnerability in Bukit, please report it privately.

**Do not open a public issue.** Instead, send details to the maintainers.

We will acknowledge your report within 7 days and aim to provide a fix within 30 days.

## Security Considerations

### Webhook (`bukit webhook`)

The webhook server accepts incoming HTTP requests and triggers GitHub `repository_dispatch` events. To use it securely:

- Always set `BUKIT_WEBHOOK_TOKEN` to authenticate incoming requests
- Use HTTPS in production deployments (e.g., behind a reverse proxy)
- Limit rate with the built-in rate limiter (10 requests per minute)
- See [guide/dev/webhook.md](guide/dev/webhook.md) for full deployment guidance

### Notion API Token

The Notion integration token is sensitive. Store it in environment variables or a secure credential store:

```bash
export BUKIT_NOTION_TOKEN=secret_xxx
```

Never commit tokens to version control.

### External Plugins

External plugins run as separate processes or WASM modules. Only use plugins from trusted sources.

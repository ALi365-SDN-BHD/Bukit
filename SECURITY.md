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

### Core and Labs Boundary

Bukit Core does not expose an in-process hook API as the stable extension
boundary. Security review for extension behavior should start from the external
process plugin path: `Bukit.PluginHost`, `Bukit.Plugin.Abstractions`,
project plugin config, and the plugin package `plugin.yaml`.

Labs features, including webhook workflows, are outside the stable Core command
registry. Treat Labs services as separate deployment surfaces and do not describe
them as Core runtime guarantees. See [guide/labs/webhook.md](guide/labs/webhook.md)
for the current Labs webhook boundary.

### External Plugins

External plugins run as separate processes under the `bukit-plugin-v1` protocol.
Only use plugins from trusted sources, and verify package manifests before
enabling them.

Plugin security review should confirm:

- `plugin.yaml` declares the expected id, protocol, platforms, entries, and
  required permissions.
- Runtime entries are selected through `Bukit.PluginHost` and hash-checked before
  invocation.
- Filesystem, environment, timeout, and output permissions are explicit and
  minimal.
- CI execution is intentional and does not bypass plugin manifest or permission
  checks.
- Reports mask secrets and avoid writing raw token values.

See [guide/dev/plugins.md](guide/dev/plugins.md) for the current plugin host
boundary.

### Secrets and Tokens

Never commit tokens, API keys, webhook shared secrets, or deploy credentials to
version control. Configuration files should name required secret sources without
embedding secret values.

Use an external secrets provider for automation and deployment, such as GitHub
Actions secrets, a deployment platform secret manager, or a local environment
manager for development. Bukit reads provider secrets from the runtime
environment; plugins receive only explicitly granted environment permissions.

See [guide/dev/config-site-yaml.md](guide/dev/config-site-yaml.md) for config
contract rules and [guide/dev/publish-deploy.md](guide/dev/publish-deploy.md)
for publish/deploy boundaries.

# Security Policy

## Supported Versions

| Version | Support status |
|---------|----------------|
| 2.0.x   | Governed for internal use; no public support SLA |
| 1.x     | Historical; no public support commitment |

## Reporting a Vulnerability

If you discover a security vulnerability in Bukit, please report it privately.

**Do not open a public issue.** Instead, send details to the maintainers.

Good-faith private reports are welcome and may be reviewed on a best-effort
basis. The project does not promise a public acknowledgement deadline,
remediation deadline, support SLA, or release timeline. See
[Bukit Core Product Positioning](docs/governance/bukit-core-product-positioning.md).

## Security Considerations

### Core Content And Output Boundaries

Current Core safety behavior includes:

- configured and explicit output cleanup share one guarded cleaner; project,
  home, filesystem root, `.git`, outside-root, symlink/reparse targets, and
  non-empty unmarked directories are refused;
- the default generated search UI treats content titles and snippets as text
  and does not pass them through an HTML interpretation sink;
- default recursive content, static, media, and report inventory paths do not
  descend through directory symlinks or reparse points.

These guarantees do not sanitize arbitrary themes, custom scripts, or
third-party plugin output, and `build.followSymlinks: true` remains limited to
supported copy paths. See
[Core Safety And Reliability](guide/user/20-core-safety-reliability.md) for the
full behavior and exclusions.

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

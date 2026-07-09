# Plugin Host Boundary

Bukit has two plugin concepts:

- Core-internal plugin interfaces under `Bukit.Engine.Abstractions.Plugins`.
- External process plugin protocol under `Bukit.PluginHost` and
  `Bukit.Plugin.Abstractions`.

Do not collapse those into one public SDK in documentation.

## External Process Flow

1. Project config is loaded by `PluginConfigLoader`.
2. Plugin source paths are validated.
3. `plugin.yaml` is loaded and checked against the configured plugin id.
4. Platform entry is selected for the current RID.
5. Entry hash is verified.
6. CI policy, permissions, and environment grants are validated.
7. The host performs handshake and runtime manifest calls.
8. Exposed commands are converted to CLI descriptors.
9. Invocation sends `PluginInvokeRequest` and renders messages, diagnostics,
   artifacts, and exit code.

## Security Model

External plugins receive only granted filesystem and environment permissions.
Secrets are masked in reports. Plugin output size and timeouts are configured
through plugin host config, not through Core build hooks.

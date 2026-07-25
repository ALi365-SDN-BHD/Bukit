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

## IndexNow Internal Plugin

`Bukit.Plugin.IndexNow` is an external process plugin, not a static Core CLI
command. A project enables it through `.bukit/plugins.yaml`, grants only the
declared filesystem/network/environment permissions, and invokes:

```text
bukit indexnow submit --change-set <path> --snapshot <output>/.bukit/publish-url-snapshot.json --site-url https://silushangxun.com/ --state-dir .cache/indexnow [--dry-run]
```

The candidate snapshot follows `publish-url-snapshot.v1`; its explicit-baseline
diff follows `publish-url-change-set.v1`. The schemas live at
`docs/schemas/publish-url-snapshot.v1.schema.json` and
`docs/schemas/publish-url-change-set.v1.schema.json`. The plugin derives the
production output root only from the required
`<output>/.bukit/publish-url-snapshot.json` layout.

`INDEXNOW_KEY` is the only accepted key source and must be explicitly granted
through the plugin environment permission. Online submission writes the public
`{key}.txt` only in the derived production output root and keeps notification
state under the exact `.cache/indexnow` state directory. Dry-run performs no
network call, key-file write, or state mutation.

The internal SRBiz install bundle is platform-specific. Its installer manifest
records Core and plugin versions, the `osx-arm64` RID, relative install targets,
and real SHA-256 archive, plugin-entry, and package hashes. This internal
artifact is not a public release and does not imply release, smoke, or audit
verification.

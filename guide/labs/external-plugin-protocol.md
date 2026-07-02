# Labs: External Plugin Protocol

Status: not Core 1.0.

The old developer guide described dynamically installed plugin hosts and
project-local plugin configuration. Core 1.0 removed that default path.

## Core Boundary

Core currently loads built-in plugins only through
`src/Bukit-Core/Bukit.Engine/Plugins/PluginRegistry.cs`.

Core strict config rejects old project-local plugin fields such as:

- `site.externalPlugins`;
- `site.externalPluginPolicy`;
- `site.externalAssemblyTrustMode`;
- `site.externalProtocolIncludeRoutedPages`.

## Historical Shape

Older drafts used config shaped like:

```yaml
site:
  externalPlugins:
    sample:
      runtime: process
      entry: plugins/sample-plugin
      hooks:
        - after-build
```

Do not copy this into Core docs or examples.

## Labs Re-Entry Requirements

Before this can become supported, Labs must own:

- plugin config types that do not depend on removed Core config fields;
- capability enforcement;
- environment isolation;
- process and/or wasm host lifecycle;
- timeout and memory policy;
- security-report schema updates;
- tests proving Core still stays built-in-only unless Labs is explicitly used.


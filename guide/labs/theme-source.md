# Labs: Remote Theme Source

Status: not Core 1.0.

The old developer guide described declaring a remote Git theme source in
`site.yaml`. Core 1.0 removed that default path.

## Core Boundary

Core currently enforces:

- `ThemeConfig` has no `Source` property;
- site-level `theme.source` is rejected by strict config validation;
- `ThemeConfig` has no site-level `Extends` property;
- remote theme source tooling is absent from `Bukit.Engine`;
- themes are local filesystem directories under `themes/<name>`.

## Historical Shape

Older drafts used a shape like:

```yaml
theme:
  source: https://example.com/theme.git@v1.0.0
  name: starter
```

Do not copy this into Core docs or examples.

## Labs Re-Entry Requirements

Before this can become a supported workflow, Labs must own:

- a config contract separate from Core `site.yaml`, or a tested Core contract
  change;
- clone/fetch/update security policy;
- lockfile and reproducibility semantics;
- path traversal and output-root safety tests;
- clear offline behavior;
- docs that do not imply current Core support.


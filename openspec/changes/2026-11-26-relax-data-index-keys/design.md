# Design: relaxed dataIndex keys

- **Keys only.** Scopes and source names are path segments in templates and config; keys are read
  with brackets, so dots and dashes are safe there.
- **Leading letter or digit.** Rules out hidden-looking or separator-only keys (`.x`, `-x`).
- **requiredKeys.** The scope cannot contain a dot, so splitting at the first dot is unambiguous.
- **One rule.** `DataIndexKey` is shared by the config validator and the engine.

# Tasks

- [x] 1 Config (internal types): `ContentConfig.Links`, `ContentLinkConfig`, `ContentLinkTargetConfig`, reader, strict fields, JSON schema.
- [x] 2 Semantic config validation: path syntax (at most 8 names), target key, `select` names, one writer per output field.
- [x] 3 `ContentLinkResolver.Apply` in `RoutePipeline` after routing, before list routes.
- [x] 4 `ContentLinkResolver.Validate` in `ContentGraphValidateStage` under `schemaFailMode`.
- [x] 5 Tests: `Bukit.Config.Tests` (config) and `Bukit.Engine.Tests` (resolver, pipeline, validate stage, hash).
- [x] 6 Docs: `04-site-yaml-config.md`, skills, CHANGELOG in three languages.

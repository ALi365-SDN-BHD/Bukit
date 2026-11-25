# Tasks

- [x] 1 Config (internal): `ContentLinkBackrefConfig`, reader, strict fields, JSON schema.
- [x] 2 Semantic validation: name, limit, select, one writer per field per collection.
- [x] 3 `ContentLinkResolver.Apply` writes backrefs in collection order; `CollectionRouteIndex.OrderEntries` shares the order.
- [x] 4 `ContentLinkResolver.Validate` reports existing backref fields on target pages.
- [x] 5 Tests: `Bukit.Config.Tests` and `Bukit.Engine.Tests` (resolver, pipeline, worker config).
- [x] 6 Docs: `04-site-yaml-config.md`, the `bukit-content-model` skill, CHANGELOG in three languages.

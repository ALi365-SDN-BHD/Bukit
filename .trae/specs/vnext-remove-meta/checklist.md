# vNext Meta Removal Checklist

- [ ] Red -> Green -> Refactor was followed for every `.cs` logic change.
- [ ] No production `.cs` change was made before a relevant failing test.
- [ ] No long-lived compatibility fallback for `Meta` was introduced.
- [ ] No `[Obsolete]` forwarding API was introduced for removed vNext surfaces.
- [ ] Unknown raw content keys fail with deterministic diagnostics unless
      declared in schema.
- [ ] Runtime modules do not read raw provider properties.
- [ ] Providers only emit raw input documents.
- [ ] Templates no longer expose `page.meta`.
- [ ] Plugin protocol v2 no longer exposes `Meta`.
- [ ] New side-effecting services, if any, have `I*` interfaces and constructor
      injection.
- [ ] Changed `.cs` files remain at or below 600 lines.
- [ ] Dependency direction still follows the Bukit project matrix.
- [ ] User-visible breaking changes are documented.
- [ ] Maintainer contracts are documented.
- [ ] Final verification commands were run and reviewed.

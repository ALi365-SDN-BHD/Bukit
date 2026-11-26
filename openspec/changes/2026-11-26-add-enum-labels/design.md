# Design: enum labels

- **Values stay public, labels internal.** `Enum` keeps `IReadOnlyList<string>`; labels are an
  internal value-to-label map, so public-api-drift is unchanged and validation reads the same list.
- **Order.** Options come from the `Enum` list, never from a map, so definition order is kept.
- **Namespace.** `site.field_enums` does not collide with `site.data.<source>`. Field scopes sit
  under `scopes`, which is why a custom enum field cannot use that name.
- **Delivery.** An internal site data projection like `site.queries`, tracked by incremental builds.

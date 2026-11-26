# Design: related field indices

- **Internal members on public records.** `Field` and `Collections` are internal with
  `JsonInclude`, following `CollectionConfig.RouteWhen`, so the public API baseline is unchanged.
  A field index keeps `Name = "field"` because `Name` is required.
- **Collection rules are additive.** Pages outside configured collections keep the site rules and
  candidates, so existing sites score the same.
- **Stable order.** Candidates are taken in `CollectionRouteIndex.OrderEntries` order and sorted by
  score with that order as the tie-break.

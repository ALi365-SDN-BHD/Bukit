## Context

Bukit injects Google Analytics at the beginning of head. The asynchronous
loader can run while HTML parsing is paused before title. Moving Analytics or
delaying config changes established consent and short-visit behavior.

## Decisions

- Inspect the cleaned final HTML with the existing title inspector and pass its
  primary normalized title through the internal render context.
- Put the title on the config script as an encoded data attribute. The fixed
  inline script reads its own attribute, keeping page-specific text outside the
  script body and preserving CSP report coverage.
- Emit one config command per destination. If the title attribute is absent,
  pass an empty options object and retain Google's document-title default.
- Treat the static title as destination config state. Clients that deliberately
  change document.title later must supply their intended title on later events.
- Increment the renderer contract version to invalidate prior incremental HTML.

## Rejected Alternatives

- An extra page_view would double count and hide the first bad event.
- Moving all Analytics to the end of head changes established ordering.
- Waiting for DOM readiness can miss short visits and reorder early events.
- Page-specific inline JavaScript expands the CSP hash set per page.

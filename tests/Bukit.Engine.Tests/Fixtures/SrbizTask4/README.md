# SRBiz Task4 fixed build-clock replay

These 74 inputs derive from approved, read-only
`2026-09-10-contract-alignment-candidate4/source`. The only changed input is
`themes/srbiz/layouts/partials/footer.html`: its year expression now uses the
existing `site.build_year` instead of Scriban `date.now`. This website repair was
separately authorized after the first Core verification found the clock gap.
The original candidate remains untouched. `provenance.json` records the candidate
manifest SHA256, source/Engine commits, original/effective footer hashes and all
74 effective file hashes. Every test execution verifies those effective hashes.
All other relations, dates, bodies, configuration, templates and static files
retain their sealed bytes. No mutable SRBiz checkout, Notion, installed CLI or
downloaded runtime is needed.

Run the existing complete Engine specialty:

```sh
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release
```

`SrbizTask4_FixedClockReplaysAllPublicBytes` copies the fixture to a unique
 temporary directory, fixes mtime to the original Task4 value (1784937600), and
uses the existing internal `TimeProvider` constructor with
`2040-12-31T16:00:00Z`. It runs root and `/docs` independently:

- cold: empty cache, clean output, incremental disabled;
- warm: retained cache, clean output, incremental enabled;
- incremental: retained cache/output, incremental enabled and verified cache hits.

All public paths and SHA256 values must agree within each BaseUrl. The existing
`PublicHashes` helper excludes only `.bukit` internal reports, build state and
the output ownership marker. Static 404, JavaScript, images, feeds, search,
projections and public time fields remain included. The four original raster/icon
assets total 984,820 bytes; preserving them retains the complete public file set
without adding runtime binaries. The copied Python assertions remain provenance
inputs; the Engine test uses C# assertions without invoking Python.

Every generated index page must render the actual copyright year 2041 in the
configured Asia/Kuala_Lumpur timezone. No probe or replacement template is added.
A subsequent build changes only the injected clock to one hour earlier: the
real footer must render 2040, and its incremental public hashes must match a
clean build at that earlier instant. The original date.now footer fails the
2041 assertion. These checks prove the clock affects public output and that
changing build year invalidates affected cached rendering.

Optional `BUKIT_SRBIZ_CLOCK_EVIDENCE` saves six public inventories and two clock
control records to a private evidence directory. This is source-version Engine
evidence. The separately checked locked Native AOT CLI has no fixed-clock
parameter. These tests do not claim historical Notion reconstruction, frozen
HTTP/performance/internal-report clocks, browser behavior or online acceptance.
Static 404 retains its authored 2026 text and original bytes; it has no dynamic
build-time year expression.

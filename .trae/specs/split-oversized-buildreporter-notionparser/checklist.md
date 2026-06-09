# Checklist

- [x] NotionPropertyParser.cs < 600 lines (duplicated methods removed) — 376 lines
- [x] BuildReporter.cs < 600 lines (security methods extracted to BuildReporterSecurity.cs) — 495 lines
- [x] BuildReporterSecurity.cs created with all extracted security methods — 340 lines
- [x] NotionPropertyParser.cs public API unchanged (ExtractFields, ExtractTitle, ExtractSlug, etc.)
- [x] Full dotnet build passes (2 pre-existing xUnit analyzer errors in RoutePathBuilderTests.cs, unrelated)
- [x] Full test suite passes — Bukit.Content.Tests: 653 passed | Bukit.Engine.Tests: 1210 passed
- [x] No new oversized files (≥600 lines) introduced
- [x] `scripts/.oversized-baseline.txt` remains empty (no new entries)

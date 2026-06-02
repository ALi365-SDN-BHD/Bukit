# Checklist

- [x] `--strict warn` → residues reported as warnings, import succeeds (exit 0)
- [x] `--strict` (no value) → residues cause failure (behavior preserved)
- [x] No `--strict` → residues reported, import succeeds (behavior preserved)
- [x] ImportModels: `StrictMode` field exists (nullable string, not bool)
- [x] HtmlDemoImporter: `ThrowIfStrictDiagnostics` only called when StrictMode is "fail"
- [x] Import report notion mode: "Seed Push Scope" section exists
- [x] Report lists pages/posts/companies/services as default push
- [x] Report lists sections/faqs/media/components as for review only
- [x] All existing `--strict` tests still pass
- [x] `dotnet test` — all tests pass with 0 failures (3,323 passed)

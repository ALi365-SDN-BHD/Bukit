# Release Checklist

Run this checklist before every public release (preview, beta, or stable).

## Documentation

- [ ] README language versions synchronized (`.md`, `.zh-CN.md`, `.ms.md`)
- [ ] `guide/user` cross-references validated
- [ ] `guide/dev` cross-references validated
- [ ] `guide/ai` prompt pack validated
- [ ] No stale references to SiteGen (old project name)
- [ ] BukitJalil boundary is clear (not listed as core)
- [ ] Skills docs (`src/skills/*`) linked from README but not duplicated

## Build

- [ ] `dotnet build bukit.slnx -c Release` passes
- [ ] `dotnet test` passes (all projects)
- [ ] Smoke scripts pass on example sites (`examples/starter/`)
- [ ] AOT build produces zero warnings

## Security

- [ ] No tokens, keys, or secrets in any documentation file
- [ ] `NOTION_TOKEN` is the only Notion auth reference in docs
- [ ] Webhook token examples use placeholders only (e.g., `YOUR_WEBHOOK_SECRET`)
- [ ] All image URLs are relative or from allowed domains

## Stability Scope

- [ ] `public-preview-scope.md` is up to date
- [ ] Preview features are clearly marked
- [ ] Roadmap does not over-promise undelivered capabilities
- [ ] Project status section (in root README) is accurate

## Version

- [ ] Version number bumped (if applicable)
- [ ] Changelog entry matches actual changes
- [ ] Breaking changes are documented with migration guidance

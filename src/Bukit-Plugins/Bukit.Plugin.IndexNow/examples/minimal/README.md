# IndexNow Plugin Minimal Fixture

This is the source example for the internal `osx-arm64` install artifact. Its
`plugin.yaml` entry hash matches the staged self-contained
`bukit-plugin-indexnow` executable recorded in
`docs/internal/seo-geo-wp1-osx-arm64.install.json`. The artifact is internal,
not a public release.

The command reads `INDEXNOW_KEY` only from the explicitly granted environment.
It derives the production output root from the required
`<output>/.bukit/publish-url-snapshot.json` path.

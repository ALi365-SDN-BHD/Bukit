# IndexNow Plugin Minimal Fixture

This is the source example for the internal `osx-arm64` install artifact. Its
`plugin.yaml` entry hash matches the staged self-contained
`bukit-plugin-indexnow` executable recorded in
`docs/internal/seo-geo-wp1-osx-arm64.install.json`. The artifact is internal,
not a public release.

In that installer manifest, `stagedEntry` is relative to `artifactRoot`, while
`packageEntry` is relative to the root of the combined `.tar.gz` package. They
name the same hashed executable in two different layouts.

The command reads `INDEXNOW_KEY` only from the explicitly granted environment.
It derives the production output root from the required
`<output>/.bukit/publish-url-snapshot.json` path.

For an online run, first invoke the command to generate `{key}.txt` in that
output, deploy the generated output to GitHub Pages, and then rerun the same
command. Submission occurs only after the public key URL returns HTTP 200 and
its response body exactly equals `INDEXNOW_KEY`.

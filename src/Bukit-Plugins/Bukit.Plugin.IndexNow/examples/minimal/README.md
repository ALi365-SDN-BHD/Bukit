# IndexNow Plugin Minimal Fixture

This is a process-plugin compatibility fixture, not an installable release.
The manifest hash is a placeholder until Task 1-07 creates the internal,
RID-specific artifact and records its real SHA-256.

The command reads `INDEXNOW_KEY` only from the explicitly granted environment.
It derives the production output root from the required
`<output>/.bukit/publish-url-snapshot.json` path.

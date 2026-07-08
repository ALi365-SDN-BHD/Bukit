# Native AOT

Bukit release artifacts may be built as Native AOT binaries. AOT work is a release and compatibility concern, not a separate user workflow.

## Maintainer Focus

- Keep command registration static and discoverable from `BukitCliSpecs.cs`.
- Avoid runtime reflection paths that cannot be rooted by tests or source generation.
- Keep config, schema, and report serialization explicit.
- Treat trimming warnings as release blockers unless a documented baseline says otherwise.
- Verify provider paths that depend on environment variables, especially Notion validation.

## Proof Points

Run the normal test suite first:

```bash
dotnet test
```

Then run the repository release or AOT gate used by the current branch. The exact command belongs to the release scripts, not this guide, because RID and artifact targets are branch and platform dependent.

## Documentation Rule

If an AOT fix changes CLI options, config fields, report shape, or runtime behavior, update the matching guide and skill file in the same change.

# Release Checklist

Use this checklist only for release tasks.

1. Confirm branch and worktree are clean except intended changes.
2. Run the fast gate.
3. Run targeted runtime tests for changed surfaces.
4. Publish Native AOT artifacts for required RIDs.
5. Smoke packaged artifacts.
6. Verify checksums and release manifests.
7. Confirm README and `guide/` links.
8. Confirm no Labs command is exposed as a Core command.

For documentation-only changes, stop after the fast gate and final diff audit.

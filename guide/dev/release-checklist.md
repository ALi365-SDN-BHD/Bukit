# Release Checklist

Use this checklist for internal artifact qualification or an explicitly
approved public release. Regular public binary releases are paused.

0. Record the release purpose:
   - `internal-artifact`, which must not publish a GitHub Release; or
   - `public-release`, which requires explicit management approval before any
     tag, upload, or publication.

1. Confirm branch and worktree are clean except intended changes.
2. Run the fast gate.
3. Run targeted runtime tests for changed surfaces.
4. Publish Native AOT artifacts for required RIDs.
5. Smoke packaged artifacts.
6. Verify checksums and release manifests.
7. Confirm README and `guide/` links.
8. Confirm no Labs command is exposed as a Core command.

For documentation-only changes, stop after the fast gate and final diff audit.

Technical success cannot upgrade `internal-artifact` to `public-release`.

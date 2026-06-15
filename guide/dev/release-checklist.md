# Release Checklist

Use this checklist for Core maintenance releases after local development gates
are green. Keep each evidence file with the release record.

1. Merge the release branch to `main`.
2. Wait for `.github/workflows/ci.yml` on `main` to complete successfully.
3. Confirm workflow evidence with `scripts/release/ci-workflow-evidence.sh` and
   preserve `TestResults/release-gate/rc-gate-evidence.md`.
4. Confirm coverage evidence and preserve
   `TestResults/coverage/coverage-summary.txt`.
5. Create the release tag from the verified commit.
6. Run the release workflow for that tag.
7. Confirm `release-assets-check.md` is present and successful.
8. Confirm `checksums.txt` is present and matches the released assets.
9. Confirm `release-manifest.json` is present and matches the released assets.
10. Download the release artifact bundle and run the local artifact smoke with
    `scripts/smoke/release-artifacts.sh <artifact-dir>`.

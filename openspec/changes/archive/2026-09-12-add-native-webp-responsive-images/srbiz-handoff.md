# SRBiz WebP handoff

This handoff does not authorize or perform an SRBiz binary, configuration,
build, deployment, or CI change.

## Core artifact

- Source HEAD: `974e032ef854b3bcf67a3834872230df7fd5c33b` plus source patch SHA-256 `9f6b513c1d07d0c91b6dfa7dbb64d525aa4752589de449053e78ac9aa7f04df4`.
- Verified Native AOT RID: `osx-arm64`.
- Binary SHA-256: `53e3bb3d9eacc46d77f7fd7acdadb50a344c132220350dbcf0bca787cdfb4f99`.
- Version: `2.0.0-ta01+974e032ef854.patch9f6b513c1d07`.

## Separately authorized SRBiz change

```diff
 theme:
   images:
     enabled: true
-    formats: []
+    formats: [webp]
     sizes: [640, 960, 1280]
     quality: 80
```

Update `scripts/check-responsive-images.sh` in the same SRBiz change because
its current contract intentionally requires `formats: []`. Do not weaken the
check to accept both states after WebP is approved.

## Acceptance

From an isolated archive of the authorized SRBiz revision, with the final Core
publish tree installed under `bukit/.bukit/tools/bukit/osx-arm64/`, the existing
`bukit/bukit` launcher preserved, and credentials supplied only through the environment:

```sh
shasum -a 256 bukit/.bukit/tools/bukit/osx-arm64/bukit
bukit/bukit build --config site.yaml --output webp-acceptance --cache-dir webp-acceptance-cache --clean --ci --no-incremental
bash scripts/check-responsive-images.sh webp-acceptance
```

Then verify at 390, 768, and 1440 CSS pixels that representative home,
insights/article, companies/company, and join pages retain layout and alt-text
behavior; supporting browsers select `image/webp`, fallback browsers use the
original `img`, all referenced files exist, and representative image transfer
bytes fall by at least 30%. Keep `formats: []` when any blocking regression or
the transfer target is not met.

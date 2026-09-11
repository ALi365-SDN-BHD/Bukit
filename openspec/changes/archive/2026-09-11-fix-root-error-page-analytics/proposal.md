# Preserve the root error document through static rendering

User authorization: confirmed in the GA4 follow-up task on 2026-09-10. This is an independent Core repair scope; no commit, push or deployment is authorized.

Static rendering currently converts root `404.html` to `404/index.html`, preventing websites from opting into the standard Analytics transform while preserving hosting error routing. Preserve the root error document in both existing static route builders. Nested `docs/404.html` and ordinary pages retain their current directory routes. Reuse existing production-only Analytics and consent handling; do not add provider code to themes.

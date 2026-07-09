#!/usr/bin/env python3
from __future__ import annotations

from pathlib import Path

required_tokens = {
    "guide/user/01-quick-start.md": [
        "## Minimal Theme Manifest",
        "name: site",
        "version: 1.0.0",
        "engine: bukit",
    ],
    "guide/user/04-site-yaml-config.md": [
        "`site.sitemapDetail.defaultPriority`",
        "`site.collections.<name>.output.archiveDetail.depth`",
        "`content.sources[].notion.propertyMap`",
        "`content.media.downloadToLocal`",
        "`content.modelSchema.canonicalMappings[]`",
        "`build.report.securityFailMode`",
        "`theme.componentValidation`",
        "`taxonomy.pinFieldBySource`",
        "`deploy.provider`",
    ],
    "guide/user/06-notion-content.md": [
        "Title: Name",
        "Slug: Slug",
        "PublishAt: PublishAt",
        "Language: Language",
        "`Title`, `Slug`, `Type`, `PublishAt`, `Language`, `I18nKey`, `Summary`, `Collection`, `SeoTitle`, `SeoDescription`, `SeoImage`, and `Canonical`",
    ],
    "guide/user/16-parameter-cheatsheet.md": [
        "`site.sitemapDetail.defaultPriority`",
        "`site.collections.<name>.filteredLists[].emptyBehavior`",
        "`content.media.blockPrivateNetworks`",
        "`content.modelSchema.requireRelationTargets`",
        "`build.report.securityFailMode`",
        "`theme.images.quality`",
        "`taxonomy.pinField`",
        "`deploy.provider`",
    ],
    "guide/dev/config-site-yaml.md": [
        "There is no sitemap",
        "object nested directly under `site`",
        "`scripts/checks/config-docs-contract.sh`",
    ],
}

forbidden_tokens = {
    "guide/user/04-site-yaml-config.md": [
        "`site.sitemap`",
        "`feed`, `sitemap`, `pagination`",
    ],
    "guide/user/06-notion-content.md": [
        "\n          title:",
        "\n          slug:",
        "\n          publishAt:",
        "\n          language:",
    ],
    "guide/user/16-parameter-cheatsheet.md": [
        "`site.sitemap`",
    ],
}

errors: list[str] = []

for file_name, tokens in required_tokens.items():
    path = Path(file_name)
    if not path.exists():
        errors.append(f"{file_name}: missing")
        continue
    text = path.read_text(encoding="utf-8")
    for token in tokens:
        if token not in text:
            errors.append(f"{file_name}: missing {token!r}")

for file_name, tokens in forbidden_tokens.items():
    path = Path(file_name)
    if not path.exists():
        continue
    text = path.read_text(encoding="utf-8")
    for token in tokens:
        if token in text:
            errors.append(f"{file_name}: forbidden {token!r}")

if errors:
    print("config docs contract failed:")
    for error in errors:
        print(f"  - {error}")
    raise SystemExit(1)

print("config docs contract OK")

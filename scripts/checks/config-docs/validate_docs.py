#!/usr/bin/env python3
from __future__ import annotations

import re
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

def property_map_errors(source: str, text: str) -> list[str]:
    record = re.search(
        r"public sealed record NotionPropertyMapConfig\s*\{(.*?)^\}",
        source, re.MULTILINE | re.DOTALL,
    )
    if record is None:
        return ["cannot extract NotionPropertyMapConfig from current source"]
    declarations = [line.strip() for line in record[1].splitlines() if line.strip()]
    properties = [re.fullmatch(r"public [\w?<>.,\[\] ]+? (\w+) \{ get; init; \}", line)
                  for line in declarations]
    if not properties or any(match is None for match in properties):
        return ["unsupported NotionPropertyMapConfig declaration; update the field extractor"]
    fields = {match[1] for match in properties if match is not None}
    rows = re.findall(
        r"^\| `(?:content\.sources\[\]\.notion\.)?propertyMap` \| (.+) \|$",
        text, re.MULTILINE,
    )
    if len(rows) != 1:
        return ["expected one propertyMap field inventory row"]
    documented = set(re.findall(r"`([A-Za-z][A-Za-z0-9_]*)`", rows[0]))
    return ([f"propertyMap missing field `{field}`" for field in sorted(fields - documented)]
            + [f"propertyMap unknown field `{field}`" for field in sorted(documented - fields)])


def validate() -> list[str]:
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

    source = Path("src/Bukit-Core/Bukit.Config/AppConfig.cs").read_text(encoding="utf-8")
    for file_name in ("guide/user/04-site-yaml-config.md", "guide/user/06-notion-content.md"):
        path = Path(file_name)
        if path.exists():
            errors.extend(f"{file_name}: {error}" for error in
                          property_map_errors(source, path.read_text(encoding="utf-8")))
    return errors


if __name__ == "__main__":
    errors = validate()
    if errors:
        print("config docs contract failed:")
        for error in errors:
            print(f"  - {error}")
        raise SystemExit(1)
    print("config docs contract OK")

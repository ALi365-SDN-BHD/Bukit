#!/usr/bin/env python3
"""Add status metadata to all SKILL.md Front Matter."""
import os

SKILLS_DIR = os.path.join(os.path.dirname(__file__), "..")

SKILL_STATUS = {
    "using-bukit": "stable", "bukit-cli-reference": "stable",
    "bukit-config": "stable", "bukit-theme": "stable",
    "bukit-templating": "stable", "bukit-design-tokens": "stable",
    "bukit-content-to-template": "beta", "bukit-notion": "stable",
    "bukit-routing": "stable", "bukit-i18n": "stable",
    "bukit-plugins-debug": "stable", "bukit-deploy": "stable",
    "bukit-clone": "beta", "bukit-seo": "stable",
    "bukit-geo": "beta", "bukit-preview": "stable",
    "bukit-dev": "stable", "bukit-webhook": "stable",
    "theme-component-system": "beta",
}

SKILL_SINCE = {k: "v3.0.0" for k in SKILL_STATUS}

SKILL_SOURCES = {
    "using-bukit": ["src/skills/using-bukit/"],
    "bukit-cli-reference": ["src/Bukit.Cli/"],
    "bukit-config": ["src/Bukit.Config/"],
    "bukit-theme": ["src/Bukit.Engine/Theme/", "src/Bukit.Cli/Commands/ThemeCommand.cs"],
    "bukit-templating": ["src/Bukit.Engine/Rendering/"],
    "bukit-design-tokens": ["src/Bukit.Engine/Theme/"],
    "bukit-content-to-template": ["src/Bukit.Engine/"],
    "bukit-notion": ["src/Bukit.Engine/Providers/Notion/", "src/Bukit.Shared/Notion/"],
    "bukit-routing": ["src/Bukit.Engine/Routing/"],
    "bukit-i18n": ["src/Bukit.Engine/I18n/"],
    "bukit-plugins-debug": ["src/Bukit.Engine/Plugins/"],
    "bukit-deploy": ["src/Bukit.Cli/Commands/DeployCommand.cs", "src/Bukit.Config/DeployConfig.cs"],
    "bukit-clone": ["src/Bukit.Cli/Commands/CloneCommand.cs"],
    "bukit-seo": ["src/Bukit.Engine/SeoDiagnostics.cs", "src/Bukit.Engine/SeoAuditReportWriter.cs"],
    "bukit-geo": ["src/Bukit.Engine/SeoDiagnostics.cs", "src/Bukit.Engine/Plugins/BuiltIn/LlmsTxtPlugin.cs"],
    "bukit-preview": ["src/Bukit.Cli/Commands/PreviewCommand.cs"],
    "bukit-dev": ["src/Bukit.Cli/Commands/DevCommand.cs"],
    "bukit-webhook": ["src/Bukit.Cli/Commands/WebhookCommand.cs"],
    "theme-component-system": ["src/Bukit.Engine/Theme/ThemeManifestV2.cs", "src/Bukit.Engine/Theme/"],
}

SKILL_GUIDE = {
    "using-bukit": ["guide/user/README.md"],
    "bukit-cli-reference": ["guide/user/12-cli-reference.md", "guide/user/16-parameter-cheatsheet.md"],
    "bukit-config": ["guide/user/04-site-yaml-config.md"],
    "bukit-theme": ["guide/user/08-themes-templates.md"],
    "bukit-templating": ["guide/user/08-themes-templates.md"],
    "bukit-design-tokens": ["guide/user/08-themes-templates.md"],
    "bukit-content-to-template": ["guide/user/08-themes-templates.md"],
    "bukit-notion": ["guide/user/06-notion-content.md"],
    "bukit-routing": ["guide/user/02-core-concepts.md", "guide/user/03-project-structure.md"],
    "bukit-i18n": ["guide/user/11-i18n-seo.md"],
    "bukit-plugins-debug": ["guide/user/10-built-in-features.md", "guide/user/14-troubleshooting.md"],
    "bukit-deploy": ["guide/user/13-deploy-github-pages.md"],
    "bukit-clone": ["guide/user/18-clone-website.md"],
    "bukit-seo": ["guide/user/11-i18n-seo.md"],
    "bukit-geo": ["guide/user/17-geo.md"],
    "bukit-preview": ["guide/user/12-cli-reference.md"],
    "bukit-dev": ["guide/user/12-cli-reference.md"],
    "bukit-webhook": ["guide/user/14-troubleshooting.md"],
    "theme-component-system": ["guide/user/08-themes-templates.md"],
}

def format_list(items):
    return "\n".join(f'  - "{item}"' for item in items)

count = 0
for entry in sorted(os.listdir(SKILLS_DIR)):
    skill_file = os.path.join(SKILLS_DIR, entry, "SKILL.md")
    if not os.path.isfile(skill_file):
        continue
    name = entry
    if name not in SKILL_STATUS:
        continue

    with open(skill_file) as f:
        content = f.read()

    if "status:" in content[:600]:
        continue

    # Insert status block right before the closing --- of front matter
    # Find second ---
    parts = content.split("---", 2)
    if len(parts) < 3:
        continue

    front_matter = parts[1]
    rest = "---" + parts[2]

    status = SKILL_STATUS[name]
    since = SKILL_SINCE[name]
    sources = SKILL_SOURCES.get(name, [])
    guides = SKILL_GUIDE.get(name, [])

    metadata = f"""
status: {status}
since: "{since}"
verified_by:
{format_list(sources)}
source_anchors:
{format_list(sources)}
guide_chapters:
{format_list(guides)}
"""

    new_content = "---" + front_matter + metadata + rest

    with open(skill_file, 'w') as f:
        f.write(new_content)
    count += 1
    print(f"  Updated: {name} ({status})")

print(f"\nUpdated {count} SKILL.md files with status metadata")

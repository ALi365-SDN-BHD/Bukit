#!/usr/bin/env python3
import os

SKILLS_DIR = os.path.join(os.path.dirname(__file__), "..")

FIXES = {
    "bukit-design-tokens": [("src/Bukit.Engine/Theme/", "src/Bukit.Engine/")],
    "bukit-i18n": [("src/Bukit.Engine/I18n/", "src/Bukit.Engine/I18nOutputMerger.cs")],
    "bukit-notion": [
        ("src/Bukit.Engine/Providers/Notion/", "src/Bukit.Engine/"),
    ],
    "bukit-routing": [("src/Bukit.Engine/Routing/", "src/Bukit.Engine/")],
    "bukit-templating": [("src/Bukit.Engine/Rendering/", "src/Bukit.Engine/")],
    "bukit-theme": [("src/Bukit.Engine/Theme/", "src/Bukit.Engine/")],
    "theme-component-system": [
        ("src/Bukit.Engine/Theme/ThemeManifestV2.cs", "src/Bukit.Engine/"),
    ],
}

for skill_name, replacements in FIXES.items():
    skill_file = os.path.join(SKILLS_DIR, skill_name, "SKILL.md")
    if not os.path.isfile(skill_file):
        continue
    with open(skill_file) as f:
        content = f.read()
    for old, new in replacements:
        content = content.replace(f'  - "{old}"', f'  - "{new}"')
    with open(skill_file, 'w') as f:
        f.write(content)
    print(f"  Fixed: {skill_name}")

print("Done")

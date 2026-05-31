#!/usr/bin/env python3
"""Fix P1-5: Refine source_anchors to specific files in SKILL.md"""
import os

SKILLS_DIR = "src/skills"
FIXES = {
    "bukit-cli-reference": [("src/Bukit.Cli/", "src/Bukit.Cli/Cli/BukitCliSpecs.cs")],
    "bukit-config": [("src/Bukit.Config/", "src/Bukit.Config/AppConfig.cs")],
    "bukit-theme": [("src/Bukit.Engine/", "src/Bukit.Cli/Commands/ThemeCommand.cs")],
    "bukit-templating": [("src/Bukit.Engine/", "src/Bukit.Engine/Plugins/BuiltIn/PagesIndexPlugin.cs")],
    "bukit-design-tokens": [("src/Bukit.Engine/", "src/Bukit.Cli/Commands/ThemeCommand.cs")],
    "bukit-content-to-template": [("src/Bukit.Engine/", "src/Bukit.Engine/ContentSchemaValidator.cs")],
    "bukit-notion": [("src/Bukit.Engine/", "src/Bukit.Engine/ContentProviderFactory.cs")],
    "bukit-routing": [("src/Bukit.Engine/", "src/Bukit.Engine/BuildPlanner.cs")],
    "bukit-plugins-debug": [("src/Bukit.Engine/Plugins/", "src/Bukit.Engine/Plugins/PluginRegistry.cs")],
    "theme-component-system": [
        ("src/Bukit.Engine/", "src/Bukit.Cli/Commands/ThemeCommand.cs"),
    ],
    "using-bukit": [("src/skills/using-bukit/", "src/skills/using-bukit/SKILL.md")],
}

count = 0
for skill_name, replacements in FIXES.items():
    skill_file = os.path.join(SKILLS_DIR, skill_name, "SKILL.md")
    if not os.path.isfile(skill_file):
        continue
    with open(skill_file) as f:
        content = f.read()
    for old, new in replacements:
        old_pattern = f'  - "{old}"'
        new_pattern = f'  - "{new}"'
        if old_pattern in content:
            content = content.replace(old_pattern, new_pattern)
            count += 1
        else:
            # Try verified_by too
            if f'  - "{old}"' in content:
                # Already in both, replace all occurrences
                pass
    with open(skill_file, 'w') as f:
        f.write(content)
    print(f"  Fixed: {skill_name}")

print(f"Done: {count} source_anchors refined")

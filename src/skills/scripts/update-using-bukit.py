#!/usr/bin/env python3
import os

script_dir = os.path.dirname(__file__)
path = os.path.join(script_dir, "..", "using-bukit", "SKILL.md")

with open(path) as f:
    lines = f.readlines()

insert_pos = 41  # after line 42 (0-indexed: 41) — before "## Bukit Skill Overview"
block = [
    "## Skill Layers (5-Layer Structure)\n",
    "\n",
    "Bukit skills are organized into five layers. Load skills in layer order:\n",
    "\n",
    "| Layer | Skills | Load Strategy |\n",
    "|---|---|---|\n",
    "| **Gateway** | using-bukit | Always first — routes to correct sub-skills |\n",
    "| **Core Reference** | bukit-cli-reference, bukit-config | Foundation — load before any build/theme/routing work |\n",
    "| **Build Authoring** | bukit-theme, bukit-templating, bukit-design-tokens, bukit-content-to-template, theme-component-system (beta) | Visual layer — after config, before content |\n",
    "| **Data / Site Features** | bukit-notion, bukit-routing, bukit-i18n, bukit-seo, bukit-geo (beta) | Content and optimization — after config |\n",
    "| **Operations / Debug** | bukit-plugins-debug, bukit-preview, bukit-dev, bukit-deploy, bukit-webhook, bukit-clone (beta) | Runtime — after build setup |\n",
    "\n",
    "Skills marked **(beta)** have stable implementations but APIs may evolve. **(experimental)** skills (if any) are not production-ready. **Do NOT** treat planned capabilities as available.\n",
    "\n",
]

for i, line in enumerate(block):
    lines.insert(insert_pos + i, line)

with open(path, 'w') as f:
    f.writelines(lines)

print("Done: using-bukit updated with 5-layer structure")

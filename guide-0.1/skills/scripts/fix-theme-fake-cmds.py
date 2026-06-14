#!/usr/bin/env python3
"""Fix P0-2: Mark fake theme commands as planned in theme-component-system/SKILL.md"""
path = 'src/skills/theme-component-system/SKILL.md'
with open(path) as f:
    content = f.read()

# Fix headings: mark as planned
content = content.replace('### `bukit theme doctor`', '### `bukit theme doctor` (planned)')
content = content.replace('### `bukit theme list-components`', '### `bukit theme list-components` (planned)')
content = content.replace('### `bukit theme export-catalog`', '### `bukit theme export-catalog` (planned)')

# Add warning notes after each heading
warning = '\n\n> **⚠️ Planned**: This command has internal implementation but is not yet registered in the CLI. It may not be available. See ThemeCommand.cs for current status.\n'

content = content.replace(
    '### `bukit theme doctor` (planned)\n\nValidates a componentized theme',
    f'### `bukit theme doctor` (planned)\n{warning}\nValidates a componentized theme'
)
content = content.replace(
    '### `bukit theme list-components` (planned)\n\nLists all sections and components',
    f'### `bukit theme list-components` (planned)\n{warning}\nLists all sections and components'
)
content = content.replace(
    '### `bukit theme export-catalog` (planned)\n\nExports the theme catalog',
    f'### `bukit theme export-catalog` (planned)\n{warning}\nExports the theme catalog'
)

# Add planned comment to command examples
content = content.replace('bukit theme doctor              # Validates active theme', 'bukit theme doctor              # (planned - not yet available in CLI)')
content = content.replace('bukit theme doctor my-theme     # Validates specific theme', 'bukit theme doctor my-theme     # (planned - not yet available in CLI)')
content = content.replace('bukit theme list-components              # Active theme', 'bukit theme list-components              # (planned - not yet available in CLI)')
content = content.replace('bukit theme list-components my-theme     # Specific theme', 'bukit theme list-components my-theme     # (planned - not yet available in CLI)')
content = content.replace('bukit theme export-catalog              # Active theme', 'bukit theme export-catalog              # (planned - not yet available in CLI)')
content = content.replace('bukit theme export-catalog my-theme     # Specific theme', 'bukit theme export-catalog my-theme     # (planned - not yet available in CLI)')
content = content.replace('bukit theme doctor --config site.yaml', 'bukit theme doctor --config site.yaml  # (planned - not yet available in CLI)')

with open(path, 'w') as f:
    f.write(content)
print("Done: theme-component-system fake commands marked as planned")

#!/usr/bin/env python3
path = 'src/skills/bukit-notion/SKILL.md'
with open(path) as f:
    lines = f.readlines()
insert_at = 85
block = [
    '\n',
    '**Property name mapping** (`propertyMap`): When your Notion database uses custom property names, map them to Bukit internal fields via `content.notion.propertyMap`. Supports 12 fields: `Title`, `Slug`, `Type`, `PublishAt`, `Language`, `I18nKey`, `Summary`, `Collection`, `SeoTitle`, `SeoDescription`, `SeoImage`, `Canonical`. Example: `propertyMap: { Title: "Page Name", Slug: "URL Slug" }`.\n',
    '\n',
    '**Filter value** (`filterValue`): Required when `filterType` is `select_equals`, `status_equals`, or `rich_text_equals`. Specifies the value to match against the filter property.\n',
    '\n',
]
for i, line in enumerate(block):
    lines.insert(insert_at + i, line)
with open(path, 'w') as f:
    f.writelines(lines)
print("Done: bukit-notion updated")

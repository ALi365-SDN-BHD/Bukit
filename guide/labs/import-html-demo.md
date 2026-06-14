# HTML Demo Conversion Labs Workflow

HTML demo conversion is not part of Bukit Core 1.0.

The Core CLI does not include an HTML demo conversion command or a Notion push command. Core users should migrate content and theme files manually or use an explicitly separate Labs tool when one exists.

## Core Alternative

1. Put reusable page text into Markdown or Notion.
2. Convert repeated HTML structure into Scriban templates under `themes/<name>/layouts/`.
3. Put CSS and JavaScript under `themes/<name>/assets/`.
4. Put root static files under `themes/<name>/static/`.
5. Validate with:

```bash
bukit config check
bukit doctor
bukit build
```

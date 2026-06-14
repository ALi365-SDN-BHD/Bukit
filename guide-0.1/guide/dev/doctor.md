# Doctor Checks

Language versions: English (current) | [简体中文](./doctor.zh-CN.md) | [Bahasa Melayu](./doctor.ms.md)

> **Note**: The authoritative doctor documentation is currently available in Chinese and Malay. This English stub provides a high-level overview. For detailed check lists, error codes, and troubleshooting, please refer to the [Chinese version](./doctor.zh-CN.md).

## Overview

`doctor` runs self-checks on your site configuration and environment:

1. `site.yaml` parse and validation
2. Content provider connectivity (Markdown dirs, Notion API with `NOTION_TOKEN`)
3. Theme and template presence
4. Output directory readiness
5. Environment variable checks

## Quick Run

```bash
dotnet run --project src/Bukit.Cli -c Release -- doctor --config site.yaml
```

Exit code 0 = all checks passed. Exit code 1 = errors found.

## Detailed Reference

For the full check list, error codes (`BKT-*`), and troubleshooting flow, see:
- [doctor.zh-CN.md](./doctor.zh-CN.md) (Chinese — authoritative)
- [doctor.ms.md](./doctor.ms.md) (Malay)

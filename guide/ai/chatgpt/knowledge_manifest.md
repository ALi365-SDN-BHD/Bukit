# GPT Knowledge Suggested List

The following files are recommended for upload as "Custom GPT" Knowledge to reduce hallucination and align the model to fields and commands.

## Required (hard constraints)

- `dosc/intent.md`: Intent contract (snake_case) and mapping rules
- `guide/dev/config-site-yaml.md`: site.yaml authoritative field reference (camelCase)
- `guide/user/12-cli-reference.md`: Copy-paste CLI commands and common parameters
- `examples/starter/site.yaml`: Minimal runnable example (markdown)
- `examples/starter/site.modules.yaml`: Multi-source + Modules (mode=data) example

## Recommended (for better diagnosis and delivery)

- `guide/user/14-troubleshooting.md`: Common errors and fixes (strongly recommended)
- `guide/user/01-quick-start.md`: Newcomer onboarding and directory conventions
- `guide/user/06-notion-content.md`: Notion usage and common pitfalls (token, fields, etc.)
- `guide/user/07-multi-source.md`: Composite sources and mode semantics
- `guide/user/09-modules-data.md`: Modules injection rules and data shapes
- `dosc/ai_guide.md`: AI delivery modes and prompt baselines

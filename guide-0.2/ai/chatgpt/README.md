# ChatGPT + Bukit: Conversational Site-Building Prompt Pack

Language versions: English (current) | [简体中文](./README.zh-CN.md) | [Bahasa Melayu](./README.ms.md)

This directory provides copy-paste prompts and instructions to move your site-creation entrypoint into ChatGPT. AI generates `intent.yaml` (recommended) or `site.yaml`, and Bukit handles deterministic validation/build.

## Two Usage Modes

### Mode A: Use directly in a ChatGPT conversation

1. Paste [system_instructions.md](./system_instructions.md) into ChatGPT system instructions (or as the first message).
2. Choose one output path:
   - Recommended: use [prompt_intent.md](./prompt_intent.md) to generate `intent.yaml`.
   - Direct config path: use [prompt_site_yaml.md](./prompt_site_yaml.md) to generate `site.yaml`.
3. Run local closed loop:

```bash
dotnet run --project src/Bukit-Core/Bukit.Cli -c Release -- intent validate intent.yaml
dotnet run --project src/Bukit-Core/Bukit.Cli -c Release -- intent apply intent.yaml --out site.yaml
dotnet run --project src/Bukit-Core/Bukit.Cli -c Release -- doctor --config site.yaml
dotnet run --project src/Bukit-Core/Bukit.Cli -c Release -- build --config site.yaml --clean --site-url https://example.com
```

4. If `intent validate` or `doctor` fails, paste the error output back to ChatGPT and use [prompt_fix_config.md](./prompt_fix_config.md) to request corrected YAML only.

### Mode B: Build a "Bukit Official GPT"

1. Set GPT instructions with [system_instructions.md](./system_instructions.md).
2. Add GPT knowledge files listed in [knowledge_manifest.md](./knowledge_manifest.md).
3. In daily usage, prefer `intent.yaml` output first, then run `intent validate/apply`.

## Safety and Validation

AI-generated configuration must always be validated before use.

1. Prefer `intent.yaml` over direct `site.yaml` generation.
2. Always run the validation loop:
   ```bash
   intent validate intent.yaml
   intent apply intent.yaml --out site.yaml
   doctor --config site.yaml
   build --config site.yaml --clean
   ```
3. If validation fails, paste only the error output back to ChatGPT.
4. Never paste secrets into ChatGPT. Always use environment variables:
   - `NOTION_TOKEN` for Notion content access
   - GitHub Secrets for CI/CD deployment
5. Do not ask AI to generate tokens, keys, absolute file paths, or unverified shell commands.

## Minimum Prerequisites

- For Notion content: set `NOTION_TOKEN` as an environment variable (never paste it in chat).
- For GitHub Pages subpath: `site.baseUrl` must start with `/`, for example `/my-repo`; root path uses `/`.

Canonical reference: [README.zh-CN.md](./README.zh-CN.md)

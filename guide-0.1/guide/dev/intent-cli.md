# intent-cli (Intent File) Implementation and Usage

Intent is the CLI implementation of the "external contract", converting structured `intent.yaml` into executable `site.yaml` and validating it.

Implementation: `src/Bukit.Cli/Commands/IntentCommand.cs`, `src/Bukit.Cli/Intent/*`

Related: [ChatGPT Prompt Pack](../ai/chatgpt/README.md), [CLI Reference](./cli.md)

## Three Subcommands

### 1) init — Interactively generate an intent file
```bash
bukit intent init --out intent.yaml
```

### 2) validate — Validate whether the intent can be applied (no file writes)
```bash
bukit intent validate intent.yaml
```
Return codes: 0=passed, 1=errors, 2=usage errors

### 3) apply — Convert intent to site.yaml
```bash
bukit intent apply intent.yaml --out site.yaml
```

## Relationship with build/multi-site
- Root `site.yaml`: use `bukit build`
- `sites/<name>.yaml`: use `bukit build --site <name>`

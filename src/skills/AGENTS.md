# Bukit Agent Knowledge Base

## When to Load Bukit Skills

Load Bukit skills when the user mentions ANY of these signals:

### Language Triggers

- English: "bukit", "using bukit", "Bukit static site", "Bukit SSG",
  "site.yaml", "Scriban", "scriban", "static site generator"
- Chinese: "使用 bukit", "用 bukit 建站", "bukit 静态站点",
  "配置 site.yaml", "Scriban 模板"
- Malay: "guna bukit", "bina laman bukit", "bukit penjana laman statik"

### File/Context Signals

- User is editing or asking about `site.yaml`
- User is editing or asking about `.scriban` or Scriban template files
- User is working with Notion-as-CMS concepts
- User mentions `bukit build`, `bukit init`, `bukit deploy`, etc.
- User is in a repository containing both `.csproj` and `site.yaml`

## How to Load

1. **Read the gateway skill first**: `src/skills/using-bukit/SKILL.md`
   - Codex loads skills natively — use your file reading tools directly.
   - There is no `Skill` tool equivalent on Codex.

2. **Follow the routing instructions** in the gateway skill to determine
   which sub-skills are needed for the user's task.

3. **Read sub-skills directly** from src/skills/&lt;skill-name&gt;/SKILL.md.

4. **ALWAYS load `bukit-cli-reference` first** before executing any
   bukit CLI command.

## Sub-Agent Dispatch

When spawning agents for Bukit tasks (requires `multi_agent = true`
in ~/.codex/config.toml or your project's .codex/config.toml):

- Read the relevant SKILL.md content.
- Pass it as task instructions to the spawned agent.
- Formulate as "Your task is to..." with the skill content wrapped
  in `<agent-instructions>` tags.
- Use `spawn_agent(message=...)` with the filled skill content.

Example message format:

```
Your task is to perform the following. Follow the instructions below exactly.

<agent-instructions>
[Full content of the relevant SKILL.md file]
</agent-instructions>

Execute this now.
```

## Environment Detection

Before operating on a Bukit site, verify the repository is a valid Bukit project:

```bash
test -f site.yaml && echo "Bukit site detected" || echo "Not a Bukit site"
```

For worktree-aware operations, run read-only git checks before proceeding:

```bash
GIT_DIR=$(cd "$(git rev-parse --git-dir)" 2>/dev/null && pwd -P)
GIT_COMMON=$(cd "$(git rev-parse --git-common-dir)" 2>/dev/null && pwd -P)
```

- `GIT_DIR != GIT_COMMON` → already in a linked worktree.
- Branch empty → detached HEAD, cannot branch/push/PR from sandbox.

## Platform Notes

- Codex has no `Skill` tool — read skill files directly with your native file tools.
- Use `update_plan` instead of `TodoWrite` for task tracking.
- Use your native shell tools instead of `Bash` / `RunCommand`.
- Skills are platform-independent: command examples use `bukit` (cross-platform);
  adapt for OS-specific suffixes (e.g., `bukit.exe` on Windows).

## Quick Reference

See `src/skills/skills-index.yaml` for the full machine-readable skill catalog
with trigger patterns, dependency chains, and common workflows.

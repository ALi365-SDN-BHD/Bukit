# Plan: Rewrite Bukit README Documentation for Public Preview

## Files in Scope

### Primary (must update)
- `README.md` (English)
- `README.zh-CN.md` (Simplified Chinese)
- `README.ms.md` (Bahasa Melayu)

### Optional (light nav-only updates if needed)
- `guide/user/README.md`
- `guide/user/README.zh-CN.md`
- `guide/user/README.ms.md`
- `guide/dev/README.md`
- `guide/dev/README.zh-CN.md`
- `guide/dev/README.ms.md`

### Out of scope (must NOT modify)
- `src/skills/**`
- Runtime source code
- Tests
- Scripts
- Examples

---

## Step-by-Step Implementation

### Step 1: Analyze Current Content Mapping

Map what each root README currently contains against the target structure to determine what to keep, what to move/remove, and what to summarize.

**Current sections in README.md (English):**
| Current Section | Target Section | Action |
|---|---|---|
| Title + logo + language selector | 1. Title + positioning | Keep, rewrite description |
| Product Ecosystem (Bukit/BukitJalil/Notes-as-CMS diagram) | 2. What is Bukit? | Keep, move to "What is Bukit?" section |
| Documents (links) | 6. Documentation | Merge with new Documentation section |
| Agent Skills (description + links) | 8. AI / Agent Workflow | Condense to 3-5 lines summary + links |
| Quick Start (4 commands) | 5. Quick Start | Keep as-is (already verified) |
| Core CLI Commands (create/build/dev/validate/clean/theme/clone/theme distribution/template commands) | — | Remove, replace with 1-line link to `guide/user` CLI docs |
| Key `site.yaml` Fields (long annotated list) | — | Remove, replace with 1-line link to config docs |
| AI Site Building (v2) links | 8. AI / Agent Workflow | Merge into AI section |
| Notion Content Source (token + schema) | 7. Notion CMS Workflow | Condense to 3-5 lines |
| GitHub Actions + GitHub Pages | 9. Deployment | Condense to 3-5 lines |
| AOT Publishing | — | Remove, replace with link to AOT docs |
| Validation Matrix | — | Remove, replace with link to contributing/testing docs |
| — (missing) | 3. Why Bukit? | Add new section |
| — (missing) | 4. Core Features | Add new section |
| — (missing) | 10. Project Status | Add new section |
| — (missing) | 11. Roadmap | Add new section |
| — (missing) | 12. Contributing | Add new section |
| — (missing) | 13. License | Add (already has LICENSE file) |

**Current sections in README.zh-CN.md (Chinese):**
| Current Section | Target Section | Action |
|---|---|---|
| Title + logo + language selector | 1. Title + positioning | Keep, rewrite description |
| 产品生态定位 | 2. What is Bukit? | Keep, move |
| 文档 (links) | 6. Documentation | Merge |
| Agent Skills | 8. AI / Agent Workflow | Condense |
| 快速开始 (quick start) | 5. Quick Start | Keep as-is |
| 命令行 (init/build/diagnose/clean/theme/webhook) | — | Remove, replace with links |
| 配置 site.yaml (long annotated list) | — | Remove, replace with links |
| 模板自定义字段 v2 (with code examples) | — | Remove, replace with links |
| v2 验收与测试 | — | Remove, replace with links |
| AI 建站 (v2) | 8. AI / Agent Workflow | Merge |
| Notion 内容源 (env vars, schema, v2 fields) | 7. Notion CMS Workflow | Condense |
| GitHub Actions + GitHub Pages | 9. Deployment | Condense |
| AOT 发布（本地） | — | Remove, replace with links |
| 验证命令矩阵 | — | Remove, replace with links |
| 性能基线 | — | Remove entirely |
| — (missing) | 3. Why Bukit? | Add |
| — (missing) | 4. Core Features | Add |
| — (missing) | 10. Project Status | Add |
| — (missing) | 11. Roadmap | Add |
| — (missing) | 12. Contributing | Add |
| — (missing) | 13. License | Add |

**Current sections in README.ms.md (Malay):**
| Current Section | Target Section | Action |
|---|---|---|
| Title + logo + language selector | 1. Title + positioning | Keep, rewrite |
| Kedudukan Ekosistem Produk | 2. What is Bukit? | Keep, move |
| Dokumen | 6. Documentation | Merge |
| Agent Skills | 8. AI / Agent Workflow | Condense |
| Mula Pantas | 5. Quick Start | Keep as-is |
| Perintah CLI Teras (create/build/check/clean/theme) | — | Remove, replace with links |
| Medan Penting site.yaml | — | Remove, replace with links |
| Bina Tapak dengan AI (v2) | 8. AI / Agent Workflow | Merge |
| Sumber Kandungan Notion | 7. Notion CMS Workflow | Condense |
| GitHub Actions + GitHub Pages | 9. Deployment | Condense |
| Penerbitan AOT | — | Remove, replace with links |
| Matriks Pengesahan | — | Remove, replace with links |
| — (missing) | 3. Why Bukit? | Add |
| — (missing) | 4. Core Features | Add |
| — (missing) | 10. Project Status | Add |
| — (missing) | 11. Roadmap | Add |
| — (missing) | 12. Contributing | Add |
| — (missing) | 13. License | Add |

---

### Step 2: Write README.md (English) with New Structure

#### Section 1: Title + One-Sentence Positioning
- Keep logo `<img>` block
- Keep language selector
- Rewrite one-liner: concise product positioning as a .NET 10 Native AOT static site generation engine for Notion-as-CMS, Markdown, AI Agent workflows, and GEO-ready websites.

#### Section 2: What is Bukit?
- Repurpose existing "Product Ecosystem" diagram
- Add 2-3 sentences explaining Bukit's role: content ingestion, route generation, Scriban rendering, SEO/GEO output, static HTML generation
- Move BukitJalil boundary description here (NOT required, clearly separate)
- Position Bukit as suitable for: company websites, documentation sites, content sites, landing pages, AI-assisted publishing workflows
- Explicitly say Bukit is NOT: a SaaS platform, full CMS backend, visual page builder, or replacement for BukitJalil

#### Section 3: Why Bukit? (NEW)
- 3-4 bullet points:
  - Native AOT: fast startup, low memory, single binary
  - Notes-as-CMS: use Notion/Markdown as content source
  - AI Agent native: skills layer for AI tools
  - GEO-ready: built-in AI search engine optimization

#### Section 4: Core Features (NEW)
- Bullet list:
  - Markdown & Notion content providers
  - Scriban template engine with layout inheritance
  - Collection-based routing with permalinks
  - Multilingual support (i18n)
  - SEO: sitemap, RSS, JSON-LD, OG, Twitter Cards
  - GEO: llms.txt, AI crawler rules, FAQ/HowTo structured data
  - Theme system with design tokens and componentized themes
  - GitHub Pages deployment
  - HMR dev server, preview server
  - Plugin system (derive-pages, after-build)
  - Incremental builds

#### Section 5: Quick Start
- Keep existing 4 commands (already verified)
- Add brief one-line explanation for each step

#### Section 6: Documentation
- Clear routing table:
  - New users → `guide/user`
  - Maintainers/contributors → `guide/dev`
  - AI Agent users → `src/skills`
  - ChatGPT / AI site building → `guide/ai/chatgpt`
  - Notion CMS → Notion guide
  - Deployment → deployment guide
- Keep concise (3-5 lines max)

#### Section 7: Notion CMS Workflow
- Condense to 3-5 lines:
  - Token via `NOTION_TOKEN` environment variable only
  - Database field conventions (Title, Slug, Published, etc.)
  - Link to full Notion guide
  - Security: never in site.yaml

#### Section 8: AI / Agent Workflow
- Condense to 3-5 lines:
  - `src/skills/` is AI Agent knowledge layer (not runtime code)
  - Intended for Codex CLI, Claude Code, Copilot CLI, Gemini CLI
  - Helps agents understand Bukit CLI, config, themes, templates, Notion, routing, i18n, deployment, SEO/GEO, debugging
  - Normal users start from `guide/user`
  - Agent users start from: `src/skills/using-bukit/SKILL.md` or `bukit-cli-reference/SKILL.md`
- Link AI site building guide if relevant

#### Section 9: Deployment
- Condense to 3-5 lines:
  - GitHub Actions workflow template at `.github/workflows/release.yml`
  - Steps: Settings → Pages → "GitHub Actions", add `NOTION_TOKEN` secret if needed, push to main
  - Link to full deployment guide

#### Section 10: Project Status (NEW)
- Public preview status
- Suitable for: local static site generation, Markdown/Notion content sites, GitHub Pages deployment, theme development, SEO/GEO validation, AI Agent workflows
- Still evolving: theme registry, clone-to-theme workflow, external plugin ecosystem, BukitJalil control panel, advanced AI intent workflow
- No over-promising

#### Section 11: Roadmap (NEW)
- Stable core: build, preview, routing, templates, Markdown, Notion, SEO/GEO
- Improving: theme ecosystem, template tooling, AI intent workflow
- Future: BukitJalil local control panel, marketplace/registry, broader knowledge-source integrations

#### Section 12: Contributing (NEW)
- Short: welcome contributions
- Link to `guide/dev` for developer guide
- Link to contributing guidelines if they exist

#### Section 13: License
- Reference existing LICENSE file

---

### Step 3: Rewrite README.zh-CN.md (Chinese)

Same structure as README.md, localized naturally:
1. 项目名称与一句话定位
2. 什么是 Bukit？
3. 为什么选择 Bukit？
4. 核心功能
5. 快速开始
6. 文档
7. Notion CMS 工作流
8. AI / Agent 工作流
9. 部署
10. 项目状态
11. 路线图
12. 参与贡献
13. 许可证

Remove all content that will be moved:
- Full CLI reference section (save "快速开始" only)
- Full `site.yaml` field list
- Template custom fields v2 examples
- v2 acceptance/testing
- AOT publishing details
- Validation matrix
- Performance baseline

Preserve:
- Logo + language selector
- Product ecosystem diagram (localized)
- Quick Start commands
- All valid documentation links

---

### Step 4: Rewrite README.ms.md (Malay)

Same structure as README.md, localized naturally:
1. Tajuk projek dan kedudukan satu ayat
2. Apa itu Bukit?
3. Kenapa Bukit?
4. Ciri Teras
5. Mula Pantas
6. Dokumentasi
7. Aliran Kerja Notion CMS
8. Aliran Kerja AI / Agent
9. Penerapan (Deployment)
10. Status Projek
11. Pelan Hala Tuju (Roadmap)
12. Menyumbang
13. Lesen

Remove all content that will be moved:
- Full CLI reference
- Full site.yaml field list
- AOT publishing
- Validation matrix

Preserve:
- Logo + language selector
- Product ecosystem diagram (localized)
- Quick Start commands
- All valid documentation links

---

### Step 5: Verify Guide README Navigation Links

Check all 6 guide README files for link consistency. Only fix broken or missing links. Do not rewrite content.

### Step 6: Validation

After editing:
1. Run `git diff -- README.md README.zh-CN.md README.ms.md` to review changes
2. Run `git diff --name-only` to confirm only intended files changed
3. Verify `src/skills/**` does NOT appear in changed files
4. Verify all 3 root READMEs have identical section ordering
5. Spot-check relative links are valid
6. Verify Quick Start commands match current CLI
7. Verify Notion token documented as env var only
8. Verify README no longer duplicates full CLI/config/AOT/testing/Skills docs
9. Verify BukitJalil clearly separated from Bukit runtime
10. Verify README reads like a public GitHub landing page

---

## Assumptions

1. `README.zh-CN.md` already has Chinese-localized product ecosystem diagram — will be preserved
2. `README.ms.md` already has Malay-localized product ecosystem diagram — will be preserved  
3. Current guide README files have correct navigation structure — no changes needed unless link validation finds issues
4. License file exists at repo root (`LICENSE` or similar) — will be referenced
5. Existing documentation links in guide READMEs point to existing files — will verify before finalizing
6. The `examples/starter` site is the canonical example and Quick Start source
7. All CLI commands in Quick Start are the verified ones from current READMEs

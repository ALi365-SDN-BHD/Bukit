# Changelog

All notable changes to Bukit will be documented in this file.

## [1.0.0] - 2026-05-05

### Added
- Initial release of Bukit, a .NET 10 Native AOT static site generator
- Markdown content source with Front Matter support
- Notion content source with database mapping and field normalization
- Multi-source content aggregation (`markdown` + `notion`)
- Scriban template engine with AOT-compatible vendored build
- Collections-driven routing system with permalink compatibility layer
- Built-in plugins: sitemap, RSS, search JSON, taxonomy, pagination, archive, pages-index
- Multi-language site support with split/merged/index modes
- Incremental build with manifest-based change detection
- Theme system with `theme list` / `theme use` commands
- Modules data source (`mode=data`) for structured content
- External plugin protocol v1/v2 (process and WASM runtimes)
- Plugin source generator for zero-reflection registration
- `doctor` diagnostic command for environment and configuration checks
- `webhook` command for Notion-to-GitHub Actions repository dispatch
- Intent system for AI-assisted site configuration
- AOT publishing with single-file output
- Performance metrics output (`--metrics`)
- Parallel rendering with configurable concurrency (`--jobs`)
- CI mode (`--ci`) with JSON structured logging
- Comprehensive user documentation (16 chapters, 3 languages)
- Developer documentation (35+ files covering architecture, CLI, plugins, etc.)
- ChatGPT prompt pack for conversational site building
- 322 unit tests across CLI, Content, Engine, and Rendering layers
- Smoke test scripts for Windows and Linux/macOS
- Documentation asset consistency check scripts

# New Developer 30-Minute Onboarding

This guide helps new contributors get productive in 30 minutes.

## 1. Prerequisites (5 min)

- .NET 10 SDK
- Git
- PowerShell (Windows) or bash (Linux/macOS)

## 2. Clone and Build (5 min)

```bash
git clone <repo-url> bukit
cd bukit
dotnet build bukit.slnx -c Release
```

## 3. Run the Example Site (5 min)

```bash
dotnet run --project src/Bukit.Cli -c Release -- doctor --config examples/starter/site.yaml
dotnet run --project src/Bukit.Cli -c Release -- build --config examples/starter/site.yaml --clean --site-url https://example.com
dotnet run --project src/Bukit.Cli -c Release -- preview --dir examples/starter/dist --port auto
```

## 4. Run Tests (5 min)

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj -c Release
dotnet test tests/Bukit.Rendering.Tests/Bukit.Rendering.Tests.csproj -c Release
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release
```

## 5. Mental Model (5 min)

Three things to remember:
1. **Content comes from somewhere** (Markdown/Notion/sources)
2. **Each content item outputs somewhere** (Routing → URL + template)
3. **A template renders it** (Scriban → HTML)

Build pipeline:
```
site.yaml → Content (Markdown/Notion) → Routing (url/template) → Rendering (Scriban → HTML) → Plugins (sitemap/rss/search) → dist/
```

## 6. Key Files to Read (5 min)

1. `src/Bukit.Cli/Program.cs` - Entry point
2. `src/Bukit.Engine/SiteEngine.cs` - Build orchestration
3. `src/Bukit.Routing/RouteGenerator.cs` - URL generation
4. `src/Bukit.Content/Markdown/MarkdownFolderProvider.cs` - Content loading
5. `src/Bukit.Engine/Plugins/PluginRunner.cs` - Plugin execution

## 7. Making Your First Change

1. Run tests to establish baseline
2. Make change
3. Run relevant tests
4. Run smoke: `pwsh ./scripts/smoke.ps1`
5. Build example site to verify

## 8. Common Workflows

### Add a CLI parameter
1. Add to `BukitCliSpecs.cs`, then `BuildCommand.cs`
2. Run smoke

### Add a content field
1. Add to `ContentItem.Fields` during content loading
2. Update templates to read it

### Add a plugin
1. Implement `IBukitPlugin` + `[BukitPlugin]` attribute
2. Place in `plugins/` or `src/plugins/`
3. Verify in build output

## 9. Go Deeper

- Architecture: [architecture.md](./architecture.md)
- Code wiki: [code-wiki.md](./code-wiki.md)
- By change type: [maintainer-entrypoints.md](./maintainer-entrypoints.md)
- Governance: [governance-checklist.md](./governance-checklist.md)

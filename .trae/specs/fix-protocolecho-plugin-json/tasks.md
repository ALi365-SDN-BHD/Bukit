# Tasks

- [x] Task 1: Fix ProtocolEchoPlugin "env" mode to produce valid JSON
  - [x] Replace manual `.Replace("\\", "\\\\")` + string interpolation with `JsonSerializer.Serialize` for the inner JSON object
  - [x] Build anonymous object and serialize to JSON string
  - [x] Embed via `JsonSerializer.Serialize` in the outer response

- [x] Task 2: Fix ExternalProtocolPluginTests to use JsonDocument.Parse
  - [x] Revert to `JsonDocument.Parse(output)` + `Assert.Equal` for all assertions

- [x] Task 3: Verify on macOS
  - [x] `dotnet test tests/Bukit.Engine.Tests -c Release --filter "DefaultEnvironmentDoesNotExpose"` — **1 passed, 0 failed**

# Task Dependencies
- Task 2 depends on Task 1
- Task 3 depends on Task 1, 2

# Bukit Labs Protocol

This directory holds legacy external protocol plugin code removed from Bukit 1.0 Core.

- `AbstractionsProtocol/`: legacy protocol DTOs and process host helpers.
- `EngineProtocol/`: legacy Engine-side external protocol runtime.
- `SamplePlugins/`: legacy sample process plugins.
- `../Bukit.Labs.Protocol.Tests/Fixtures/`: legacy protocol test fixtures.

The code is intentionally not wired into Core build, Core config, or Core CLI. Re-enabling it requires a Labs-owned config contract and project file so dependency direction remains:

```text
Labs -> Core
Core -X-> Labs
```

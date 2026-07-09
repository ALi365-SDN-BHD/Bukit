# Dev Server

`DevCommand` starts a build-backed LiveReload development server.

## Defaults

| Setting | Default |
|---|---|
| host | `localhost` |
| port | `35729` |
| watch | enabled |

Options are `--config`, `--site`, `--host`, `--port`, `--output`,
`--no-watch`, `--allow-lan`, and `--public`.

## LAN Safety

Binding to non-loopback hosts requires `--allow-lan` or `--public`. The command
warns when exposing the server outside localhost.

## Components

`DevFileWatcher` watches input files, `DevServerHost` serves output,
`DevWebSocketHub` broadcasts reload events, and `DevRequestHandler` injects the
client script for HTML responses.

# Development Notes

## Projects

- `CodexStatus.Core`: shared models, reducers, JSON IO, aggregation, settings, and hook installer.
- `CodexStatus.Hook`: silent console app invoked by Codex hooks.
- `CodexStatus.Tray`: WPF tray app and floating pill.
- `CodexStatus.ExecAdapter`: optional JSONL adapter for `codex exec --json`.
- `CodexStatus.Tests`: xUnit coverage for core behavior.

## Backends

The UI consumes normalized `AggregatedStatus` only.

- Hooks backend: default for normal Codex app and CLI usage.
- Exec JSON backend: explicit wrapper usage, for example `codex exec --json "..." | CodexStatus.ExecAdapter.exe`.
- App-server backend: future richest integration.

Core includes:

- `IStatusEventSource`
- `HookStatusEventSource`
- `ExecJsonStatusEventSource`
- `AppServerStatusEventSource`

The app-server source is a placeholder in v1. It should write the same normalized session and aggregate files when implemented.

## Local Build

```powershell
dotnet build CodexStatus.sln
dotnet test CodexStatus.sln
```

## Hook Safety

Hooks must remain fail-open:

- no stdout by default
- stderr only with `--debug`
- exit `0` for malformed input after logging locally
- no transcript parsing in v1

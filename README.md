# Codex Status for Windows

Codex Status is a Windows tray utility for watching Codex lifecycle status. It shows a notification-area icon and a small floating pill above the taskbar, using normalized state written by Codex hooks.

## What It Does

- Reads Codex lifecycle hook JSON from `CodexStatus.Hook.exe`.
- Writes local state under `%USERPROFILE%\.codex\statusbar` by default.
- Shows a tray icon for idle, active, approval, done, failed, and stale states.
- Shows a draggable floating WPF pill with the current status and elapsed time.
- Shows the latest hook stdin payload fields in the sessions flyout.
- Provides a tray menu for settings, status folder access, hook reinstall, and quit.

Codex Status does not scrape private reasoning, transcript files, or Codex UI internals. It uses hook payloads and normalized local state only. `transcript_path` is stored only as optional metadata when present.

## Build

Requires .NET 10 SDK on Windows.

```powershell
.\scripts\build.ps1
```

## Publish

```powershell
.\scripts\publish.ps1
```

The publish script creates self-contained win-x64 builds under `artifacts\publish`.

## Basic Install Flow

1. Build or publish the app.
2. Run `CodexStatus.Tray.exe`.
3. Open the tray menu and choose `Reinstall Codex hooks`.
4. Open Codex and approve or trust the hook if prompted.

Codex may require hook trust review. Codex Status does not bypass hook trust and does not use dangerous bypass flags.

## Privacy

- Local-only state.
- State files are stored under the user profile.
- Hook payload field previews are stored locally for tray display.
- Command previews can be hidden in settings.
- Secret redaction is best-effort and enabled by default.

## Known Limitations

- The hook backend does not see every internal Codex UI state.
- Some non-shell and non-MCP tool activity may appear as generic tool usage in v1.
- WSL Codex CLI usage needs extra setup. See [docs/wsl.md](docs/wsl.md).
- The app-server adapter is a future integration point, not implemented in v1.

## Current Status

This first pass includes the solution scaffold, core reducer and aggregation logic, hook executable, exec JSON adapter scaffold, WPF tray app, floating pill, settings UI, hook installer, tests, and docs.

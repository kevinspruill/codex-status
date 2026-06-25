# Codex Hooks

`CodexStatus.Hook.exe` is intended to be invoked by Codex lifecycle hooks. It reads JSON from stdin, writes no stdout, and exits `0` even for malformed input after logging locally.

## State Directory

Default:

```text
%USERPROFILE%\.codex\statusbar
```

Override:

```powershell
CodexStatus.Hook.exe --event UserPromptSubmit --state-dir C:\path\to\state
```

## Installed Events

The tray hook installer adds handlers for:

- `SessionStart`
- `UserPromptSubmit`
- `PreToolUse`
- `PermissionRequest`
- `PostToolUse`
- `PreCompact`
- `PostCompact`
- `SubagentStart`
- `SubagentStop`
- `Stop`

Matcher values are used for events that need them, such as `PreToolUse`, `PermissionRequest`, `PostToolUse`, subagent events, and compact events.

## Trust

Codex may require hook trust review after `hooks.json` changes. Codex Status does not approve, deny, trust, or bypass hooks automatically.

## Failure Behavior

The hook should not break Codex work:

- malformed JSON is logged to `codex-status.log`
- stdout is silent
- stderr is used only with `--debug`
- exit code remains `0`

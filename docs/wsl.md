# WSL

Windows-native Codex app and Codex CLI usage works by default because the hook installer targets the Windows Codex home:

```text
C:\Users\<you>\.codex
```

WSL Codex CLI may use a separate Linux home directory and a different `~/.codex`. For v1, the recommended approach is to point WSL Codex at the Windows Codex home mounted into WSL:

```bash
export CODEX_HOME=/mnt/c/Users/<you>/.codex
```

Then install hooks from the Windows tray app.

Notes:

- The hook installer targets Windows-native Codex first.
- WSL path and process bridging is not implemented in v1.
- TODO: add a Linux/WSL hook bridge for users who want separate WSL state and Windows UI display.

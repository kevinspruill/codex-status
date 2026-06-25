# State Schema

Schema version: `1`

Default state directory:

```text
%USERPROFILE%\.codex\statusbar
```

Files:

- `state.json`: aggregated state consumed by the tray app
- `sessions\<session_id>.json`: latest state for each session
- `events.jsonl`: compact debug event log
- `codex-status.log`: local hook/app log

## AgentStatusSnapshot

Important fields:

- `schemaVersion`
- `source`
- `backend`
- `sessionId`
- `turnId`
- `agentId`
- `cwd`
- `repoName`
- `model`
- `state`
- `label`
- `detail`
- `toolName`
- `commandPreview`
- `approvalReason`
- `activeSubagentCount`
- `waitingOnApproval`
- `startedAt`
- `updatedAt`
- `completedAt`
- `lastHookEventName`
- `lastRawEventType`
- `error`
- `payloadFields`
- `metadata`

`state` is serialized as a string value from `AgentDisplayState`.

`payloadFields` contains redacted, display-oriented previews of the latest hook
stdin payload, flattened into dotted keys such as `tool_input.command` or
`tool_response.exit_code`. Complex objects and arrays are summarized at their
own key and expanded into child keys when present.

## AggregatedStatus

Fields:

- `schemaVersion`
- `updatedAt`
- `primary`
- `sessions`
- `activeCount`
- `waitingApprovalCount`
- `failedCount`

Primary selection:

1. Waiting-for-approval sessions, most recently updated first.
2. Active running/thinking/tool sessions, most recently updated first.
3. Most recently updated done or failed session.

The tray may display stale or idle states without overwriting `state.json`.

# Hook payload keys (Cursor 3.27.22)

Verified from `.ai-log/payloads/*.json` after one agent turn that read `README.md`, wrote `scratch.txt`, ran `dotnet --version`, and called Playwright `browser_navigate` on `about:blank`.

The dumper keeps one file per event and overwrites it. Keys below are from the captured payloads. `beforeShellExecution.json` was overwritten by a later shell call; `afterShellExecution.json` is the `dotnet --version` call.

## Working directory

Hook commands use a relative path (`python .cursor/hooks/dump.py ...`). The dumps were written to `<repo>/.ai-log/payloads/`, so the hook process cwd is the workspace root (`c:\My Files\ai-sdlc-demo-app`).

`beforeShellExecution` has a `cwd` field. On the captured call it was an empty string, so do not trust `cwd` for the log path. `afterShellExecution` has no `cwd` field. `workspace_roots` is present on every event (`/c:/My Files/ai-sdlc-demo-app`).

## Keys by event

Common on every captured event: `conversation_id`, `generation_id`, `model`, `session_id`, `hook_event_name`, `cursor_version`, `workspace_roots`, `user_email`, `transcript_path`.

| What | Real key | Event | Notes |
|---|---|---|---|
| Conversation / session id | `conversation_id` and `session_id` | all | Same value on this run |
| Generation id | `generation_id` | all | |
| Model | `model` | all | `beforeSubmitPrompt` also has `model_id` and `model_params`. Prompt event `model` was `grok-4.7-medium-fast`; other events used `grok-4.7` |
| Prompt text | `prompt` | `beforeSubmitPrompt` | Also `attachments` (array of `{type, file_path}`), `composer_mode` |
| File path | `file_path` | `beforeReadFile`, `afterFileEdit` | Read also has `content` (file text) and `tool_use_id`. Edit also has `edits` (`old_string`, `new_string`) |
| Shell command | `command` | `beforeShellExecution`, `afterShellExecution` | |
| Shell cwd | `cwd` | `beforeShellExecution` only | Empty string in the captured payload |
| Exit code | *(absent)* | `afterShellExecution` | No exit-code field. Present instead: `output`, `duration`, `sandbox`, `tool_use_id` |
| Output | `output` | `afterShellExecution` | Stdout text |
| MCP server | `mcp_server_name` | `afterMCPExecution` | Value was `playwright` |
| MCP tool | `tool_name` | `afterMCPExecution` | Also `tool_input` (JSON string), `result_json`, `duration`, `tool_use_id` |
| Transcript path | `transcript_path` | all | Absolute path to the session `.jsonl` |

## Not produced by this run

No files for `subagentStart`, `subagentStop`, `afterAgentResponse`, or `stop`. Sub-agent name, type, task, and status keys were not observed. Check those again when a sub-agent actually runs (task 09).

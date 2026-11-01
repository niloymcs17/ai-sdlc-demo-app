# Reviewer delegation and read-only check

## Delegation

The task prompt was: list the files under `.cursor/` and stop. Do not review anything.

Launching a sub-agent with type `reviewer` was rejected. This session can start only `generalPurpose`, `explore`, `cursor-guide`, `ci-investigator`, `bugbot`, `security-review`, and `best-of-n-runner`. `.cursor/agents/reviewer.md` is not one of those types, so `/reviewer` did not run.

The fallback was an `explore` sub-agent with the reviewer instructions pasted in. The hook recorded that run, not a reviewer run.

Last matching log lines:

- `20261101T174728Z-subagent_start` — `agent_name`: `explore`
- `20261101T174737Z-subagent_stop` — `agent_name`: `explore`

No `subagent_start` or `subagent_stop` line has `agent_name: reviewer`.

Record this limitation in ASSIGNMENT.md: custom agents were not selectable from this chat, so the role was run as a separate built-in sub-agent with the reviewer instructions pasted in.

## Write attempt

The same explore sub-agent was asked to create `test.txt` in the repo root. It created the file with the contents `reviewer write attempt`. The write was not refused.

That does not show that the reviewer is read-only. The file that sets `readonly: true` was not the agent that ran. `test.txt` was deleted after the check.

## What is in place

`reviewer.md` sets `readonly: true` and says the agent must not edit files. Cursor applies that only when the reviewer agent itself is the one running. Re-check from a new Agent chat with `/reviewer` before tasks 11, 12, 18, and 20.

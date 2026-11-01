#!/usr/bin/env python3
import datetime
import json
import os
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
LOG_DIR = ROOT / ".ai-log"

SECRET = re.compile(
    r"(sk-[A-Za-z0-9_\-]{10,}|ghp_[A-Za-z0-9]{20,}|Bearer\s+[A-Za-z0-9._\-]+|ChangeMe123!)"
)
TEST_CMD = re.compile(r"dotnet (test|stryker)|playwright", re.IGNORECASE)
DOTNET_SUMMARY = re.compile(
    r"Failed:\s*(\d+),\s*Passed:\s*(\d+),\s*Skipped:\s*(\d+),\s*Total:\s*(\d+)"
)
MUTATION_SCORE = re.compile(r"The final mutation score is\s+([0-9.]+)\s*%")
PLAYWRIGHT_PASSED = re.compile(r"(\d+)\s+passed")
PLAYWRIGHT_FAILED = re.compile(r"(\d+)\s+failed")


def scrub(value):
    if not isinstance(value, str):
        return value
    return SECRET.sub("[REDACTED]", value)


def pick(data, *keys):
    for key in keys:
        if data.get(key) not in (None, ""):
            return data[key]
    return None


def git_head():
    try:
        return subprocess.check_output(
            ["git", "rev-parse", "--short", "HEAD"],
            text=True,
            stderr=subprocess.DEVNULL,
            cwd=ROOT,
        ).strip()
    except Exception:
        return None


def allow(event):
    if event == "prompt":
        print(json.dumps({"continue": True}))
    elif event == "read":
        print(json.dumps({"permission": "allow"}))


def append_summary(line):
    LOG_DIR.mkdir(parents=True, exist_ok=True)
    with open(LOG_DIR / "test-summary.txt", "a", encoding="utf-8") as summary_file:
        summary_file.write(line + "\n")


def record_shell(entry, payload):
    command = pick(payload, "command") or ""
    output = str(pick(payload, "output") or "")
    entry["tools_used"] = ["shell"]
    if not TEST_CMD.search(command):
        entry["shell_command"] = scrub(command)[:200]
        return

    tests_run = {
        "command": scrub(command),
        "exit_code": pick(payload, "exit_code"),
        "result_excerpt": scrub(output)[-600:],
    }
    commit = entry["commit"]
    dotnet_match = DOTNET_SUMMARY.search(output)
    if dotnet_match and re.search(r"dotnet\s+test", command, re.IGNORECASE):
        failed = int(dotnet_match.group(1))
        passed = int(dotnet_match.group(2))
        skipped = int(dotnet_match.group(3))
        total = int(dotnet_match.group(4))
        tests_run["passed"] = passed
        tests_run["failed"] = failed
        append_summary(
            f"TESTS: passed={passed} failed={failed} skipped={skipped} total={total} commit={commit}"
        )
    mutation_match = MUTATION_SCORE.search(output)
    if mutation_match and re.search(r"dotnet\s+stryker", command, re.IGNORECASE):
        append_summary(f"MUTATION: score={mutation_match.group(1)} commit={commit}")
    if re.search(r"playwright", command, re.IGNORECASE):
        passed_match = PLAYWRIGHT_PASSED.search(output)
        failed_match = PLAYWRIGHT_FAILED.search(output)
        if passed_match or failed_match:
            passed = int(passed_match.group(1)) if passed_match else 0
            failed = int(failed_match.group(1)) if failed_match else 0
            tests_run["passed"] = passed
            tests_run["failed"] = failed
            append_summary(
                f"TESTS: passed={passed} failed={failed} skipped=0 total={passed + failed} commit={commit}"
            )
    entry["tests_run"] = tests_run


def build_entry(event, payload):
    now = datetime.datetime.now(datetime.timezone.utc)
    entry = {
        "id": f"{now:%Y%m%dT%H%M%SZ}-{event}",
        "timestamp": f"{now:%Y-%m-%dT%H:%M:%SZ}",
        "session_id": pick(payload, "conversation_id", "session_id"),
        "generation_id": payload.get("generation_id"),
        "source": "hook-captured",
        "ai_tool": "Cursor",
        "model": pick(payload, "model", "model_id") or os.environ.get("AI_MODEL", "unknown"),
        "mode": os.environ.get("AI_MODE", "agent"),
        "stage": os.environ.get("AI_STAGE"),
        "event": event,
        "commit": git_head(),
    }
    skip_log = False

    if event == "prompt":
        entry["request"] = scrub(pick(payload, "prompt"))
    elif event == "read":
        path = pick(payload, "file_path") or ""
        normalised = path.replace("\\", "/")
        if ".cursor/skills/" in normalised:
            skill = normalised.split(".cursor/skills/")[1].split("/")[0]
            entry["skills_used"] = [{"name": skill, "type": "custom", "invocation": "automatic"}]
        else:
            skip_log = True
    elif event == "edit":
        path = pick(payload, "file_path")
        entry["tools_used"] = ["edit"]
        entry["files_changed"] = [path]
        entry["artifacts"] = [path]
    elif event == "shell":
        record_shell(entry, payload)
    elif event == "mcp":
        entry["mcp_tools_used"] = [{
            "server": pick(payload, "mcp_server_name"),
            "tool": pick(payload, "tool_name"),
        }]
    elif event in ("subagent_start", "subagent_stop"):
        # subagentStart/Stop did not fire during the payload dump; these names follow the guide until task 09.
        entry["agent_type"] = "sub-agent"
        entry["agent_name"] = pick(payload, "subagent_type", "subagent_name")
        entry["subagent_id"] = payload.get("subagent_id")
        entry["parent_conversation_id"] = payload.get("parent_conversation_id")
        entry["subagents_used"] = [{
            "name": entry["agent_name"],
            "type": "custom",
            "task": scrub(pick(payload, "task", "description", "prompt")),
            "status": pick(payload, "status") or ("started" if event == "subagent_start" else "completed"),
        }]
    elif event == "stop":
        entry["status"] = pick(payload, "status")
        entry["transcript_path"] = payload.get("transcript_path")

    return entry, skip_log


def main():
    event = sys.argv[1] if len(sys.argv) > 1 else "unknown"
    try:
        raw = sys.stdin.buffer.read().decode("utf-8", errors="replace")
        try:
            payload = json.loads(raw) if raw.strip() else {}
        except Exception:
            payload = {}
        if not isinstance(payload, dict):
            payload = {}

        entry, skip_log = build_entry(event, payload)
        if not skip_log:
            LOG_DIR.mkdir(parents=True, exist_ok=True)
            with open(LOG_DIR / "ai-interactions.jsonl", "a", encoding="utf-8") as log_file:
                log_file.write(json.dumps(entry, ensure_ascii=False) + "\n")
    except Exception:
        pass
    allow(event)


if __name__ == "__main__":
    main()

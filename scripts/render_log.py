#!/usr/bin/env python3
import json
from collections import Counter
from pathlib import Path

LOG_DIR = Path(__file__).resolve().parents[1] / ".ai-log"


def load_jsonl(name):
    path = LOG_DIR / name
    if not path.exists():
        return []
    rows = []
    for line in path.read_text(encoding="utf-8").splitlines():
        if line.strip():
            rows.append(json.loads(line))
    return rows


def cell(value):
    text = "" if value is None else str(value)
    return text.replace("|", "\\|").replace("\n", " ")


def labelled(items, key):
    names = []
    for item in items or []:
        if isinstance(item, dict) and item.get(key):
            names.append(str(item[key]))
        elif isinstance(item, str):
            names.append(item)
    return names


def joined(values):
    return ", ".join(values)


def request_text(row):
    if row.get("request"):
        return row["request"]
    if row.get("shell_command"):
        return row["shell_command"]
    for agent in row.get("subagents_used") or []:
        if isinstance(agent, dict) and agent.get("task"):
            return agent["task"]
    return row.get("human_decision") or ""


def tests_text(row):
    tests_run = row.get("tests_run") or {}
    if "passed" in tests_run or "failed" in tests_run:
        return f"{tests_run.get('passed', 0)}/{tests_run.get('failed', 0)}"
    return ""


def last_note(notes):
    return notes[-1] if notes else {}


def chronological_rows(interactions, annotations):
    ids = {row.get("id") for row in interactions}
    rows = list(interactions)
    for note in annotations:
        if note.get("event") == "manual" and note.get("id") not in ids:
            rows.append(note)
    return sorted(rows, key=lambda row: row.get("timestamp") or "")


def main():
    interactions = load_jsonl("ai-interactions.jsonl")
    annotations = load_jsonl("human-annotations.jsonl")
    by_ref = {}
    for note in annotations:
        by_ref.setdefault(note.get("ref"), []).append(note)

    rows = chronological_rows(interactions, annotations)
    outcomes = Counter(note.get("outcome") for note in annotations if note.get("outcome"))
    modes = Counter()
    skills, subagents, mcp_tools = set(), set(), set()
    for row in rows:
        note = last_note(by_ref.get(row.get("id"), []))
        modes[note.get("mode") or row.get("mode") or "unset"] += 1
        skills.update(labelled(row.get("skills_used"), "name"))
        subagents.update(labelled(row.get("subagents_used"), "name"))
        if row.get("agent_name"):
            subagents.add(row["agent_name"])
        for tool in row.get("mcp_tools_used") or []:
            if isinstance(tool, dict):
                mcp_tools.add("/".join(part for part in (tool.get("server"), tool.get("tool")) if part))
    interventions = [note for note in annotations if note.get("human_decision")]
    limitations = []
    for note in annotations:
        limitation = note.get("limitation")
        if limitation and limitation not in limitations:
            limitations.append(limitation)

    lines = ["# AI interaction log", "", "## Summary", "",
             "| Metric | Value |", "| --- | --- |",
             f"| Total interactions | {len(rows)} |",
             "| Outcome | " + cell(joined(f"{name}={outcomes[name]}" for name in ("success", "partial", "rework"))) + " |",
             "| Mode | " + cell(joined(f"{name}={count}" for name, count in sorted(modes.items()))) + " |",
             f"| Skills used | {cell(joined(sorted(skills)) or 'none')} |",
             f"| Sub-agents used | {cell(joined(sorted(subagents)) or 'none')} |",
             f"| MCP tools used | {cell(joined(sorted(mcp_tools)) or 'none')} |",
             f"| Human interventions | {len(interventions)} |",
             "", "## Chronological", "",
             "| Time | Agent | Mode | Stage | Request | Tools | Files | Tests | Outcome |",
             "| --- | --- | --- | --- | --- | --- | --- | --- | --- |"]
    for row in rows:
        note = last_note(by_ref.get(row.get("id"), []))
        request = request_text(row)[:80]
        tools = joined(row.get("tools_used") or labelled(row.get("mcp_tools_used"), "tool"))
        files = joined(row.get("files_changed") or note.get("human_changes") or [])
        lines.append("| " + " | ".join(cell(value) for value in [
            row.get("timestamp"),
            note.get("agent") or row.get("agent_name") or row.get("ai_tool"),
            note.get("mode") or row.get("mode"),
            note.get("stage") or row.get("stage"),
            request,
            tools,
            files,
            tests_text(row),
            note.get("outcome"),
        ]) + " |")

    lines += ["", "## Human oversight", ""]
    if not interventions:
        lines.append("No human decisions recorded.")
    for note in interventions:
        lines += [f"### {cell(note.get('ref'))}", "",
                  f"- Decision: {note.get('human_decision')}",
                  f"- Verification: {note.get('action_verification') or 'none'}",
                  f"- Limitation: {note.get('limitation') or 'none'}", ""]

    lines += ["## Limitations", ""]
    lines += [f"- {limitation}" for limitation in limitations] or ["None recorded."]
    lines.append("")

    LOG_DIR.mkdir(parents=True, exist_ok=True)
    output = LOG_DIR / "ai-interactions.md"
    output.write_text("\n".join(lines), encoding="utf-8")
    print(f"wrote {output}")


if __name__ == "__main__":
    main()

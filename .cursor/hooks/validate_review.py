#!/usr/bin/env python3
"""Stop hook: reviews that start with REVIEW-FINDINGS: must use the findings table."""
import json
import sys
from pathlib import Path

HEADER = "| # | Finding | Severity"
MARKER = "REVIEW-FINDINGS:"
RETRY_CAP = "Reformat your review"
FOLLOWUP = (
    "Reformat your review using the review-diff skill table: "
    "| # | Finding | Severity (high/med/low) | Location (file:line) "
    "| Suggested fix | Verdict | Reason |"
)


def text_from_message(message):
    content = message.get("content") if isinstance(message, dict) else None
    if isinstance(content, str):
        return content
    if not isinstance(content, list):
        return ""
    parts = []
    for part in content:
        if isinstance(part, str):
            parts.append(part)
        elif isinstance(part, dict) and part.get("type") == "text":
            parts.append(part.get("text") or "")
    return "\n".join(parts)


def last_assistant_text(transcript_path):
    """Only the last assistant turn. An earlier good review must not hide a later bad one."""
    last = ""
    with open(transcript_path, encoding="utf-8") as handle:
        for line in handle:
            try:
                entry = json.loads(line)
            except Exception:
                continue
            if not isinstance(entry, dict) or entry.get("role") != "assistant":
                continue
            message = entry.get("message")
            if not isinstance(message, dict):
                continue
            last = text_from_message(message)
    return last


def main():
    raw = sys.stdin.buffer.read().decode("utf-8", errors="replace")
    payload = json.loads(raw) if raw.strip() else {}
    if not isinstance(payload, dict):
        return
    transcript_path = payload.get("transcript_path") or ""
    if not transcript_path:
        return
    path = Path(transcript_path)
    if not path.is_file():
        return
    transcript = path.read_text(encoding="utf-8")
    # One follow-up per transcript. A second stop must not ask again.
    if RETRY_CAP in transcript:
        return
    last = last_assistant_text(path)
    if RETRY_CAP in last:
        return
    if MARKER not in last or HEADER in last:
        return
    print(json.dumps({"followup_message": FOLLOWUP}))


if __name__ == "__main__":
    try:
        main()
    except Exception:
        pass

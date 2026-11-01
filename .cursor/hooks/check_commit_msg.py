#!/usr/bin/env python3
"""beforeShellExecution: agent git commits need a WHY body."""
import json
import sys
from pathlib import Path

DENY = {
    "permission": "deny",
    "user_message": "Commit blocked: message needs a body explaining WHY.",
    "agent_message": "Add a second -m with the reason for the change (at least 20 characters), then retry.",
}
ALLOW = {"permission": "allow"}
MIN_BODY = 20


def allow():
    print(json.dumps(ALLOW))


def deny():
    print(json.dumps(DENY))


def tokenize(command):
    tokens = []
    current = []
    quote = None
    index = 0
    while index < len(command):
        char = command[index]
        if quote:
            if char == quote:
                quote = None
            else:
                current.append(char)
            index += 1
            continue
        if char in ('"', "'"):
            quote = char
            index += 1
            continue
        if char == "\\" and index + 1 < len(command):
            current.append(command[index + 1])
            index += 2
            continue
        if char.isspace():
            if current:
                tokens.append("".join(current))
                current = []
            index += 1
            continue
        current.append(char)
        index += 1
    if current:
        tokens.append("".join(current))
    return tokens


def commit_args(tokens):
    for index, token in enumerate(tokens):
        name = token.replace("\\", "/").rsplit("/", 1)[-1].lower()
        if name in ("git", "git.exe") and index + 1 < len(tokens) and tokens[index + 1] == "commit":
            return tokens[index + 2 :]
    return None


def message_parts(args):
    messages = []
    message_file = None
    index = 0
    while index < len(args):
        token = args[index]
        if token in ("-m", "--message") and index + 1 < len(args):
            messages.append(args[index + 1])
            index += 2
            continue
        if token.startswith("--message="):
            messages.append(token.split("=", 1)[1])
            index += 1
            continue
        if token.startswith("-") and not token.startswith("--") and "m" in token[1:]:
            rest = token[token.index("m") + 1 :]
            if rest:
                messages.append(rest)
            elif index + 1 < len(args):
                messages.append(args[index + 1])
                index += 2
                continue
            index += 1
            continue
        if token in ("-F", "--file") and index + 1 < len(args):
            message_file = args[index + 1]
            index += 2
            continue
        if token.startswith("--file="):
            message_file = token.split("=", 1)[1]
            index += 1
            continue
        if token.startswith("-F") and len(token) > 2:
            message_file = token[2:]
            index += 1
            continue
        index += 1
    amend_no_edit = "--amend" in args and "--no-edit" in args
    return messages, message_file, amend_no_edit


def file_has_body(path):
    try:
        text = Path(path).read_text(encoding="utf-8")
    except OSError:
        return False
    lines = [line for line in text.splitlines() if not line.startswith("#")]
    while lines and not lines[-1].strip():
        lines.pop()
    blank_at = next((i for i, line in enumerate(lines) if not line.strip()), None)
    if blank_at is None:
        return False
    body = "\n".join(lines[blank_at + 1 :]).strip()
    return len(body) >= MIN_BODY


def evaluate(command):
    args = commit_args(tokenize(command))
    if args is None:
        return ALLOW
    messages, message_file, amend_no_edit = message_parts(args)
    if amend_no_edit and not messages:
        return ALLOW
    if message_file is not None and not messages:
        if file_has_body(message_file):
            return ALLOW
        return DENY
    if len(messages) < 2 or len(messages[1].strip()) < MIN_BODY:
        return DENY
    return ALLOW


def main():
    raw = sys.stdin.buffer.read().decode("utf-8", errors="replace")
    payload = json.loads(raw) if raw.strip() else {}
    if not isinstance(payload, dict):
        allow()
        return
    decision = evaluate(payload.get("command") or "")
    print(json.dumps(decision))


if __name__ == "__main__":
    try:
        main()
    except Exception:
        allow()

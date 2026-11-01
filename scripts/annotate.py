#!/usr/bin/env python3
import argparse
import datetime
import json
from pathlib import Path

LOG_DIR = Path(__file__).resolve().parents[1] / ".ai-log"
INTERACTIONS = LOG_DIR / "ai-interactions.jsonl"
ANNOTATIONS = LOG_DIR / "human-annotations.jsonl"
ATTACH_EVENTS = {"prompt", "stop"}


def split_csv(value):
    if not value:
        return []
    return [part.strip() for part in value.split(",") if part.strip()]


def latest_interaction_id(path):
    chosen = None
    with path.open(encoding="utf-8") as handle:
        for line in handle:
            if not line.strip():
                continue
            entry = json.loads(line)
            if entry.get("event") in ATTACH_EVENTS and entry.get("id"):
                chosen = entry["id"]
    if not chosen:
        raise SystemExit("no prompt or stop entry to attach to")
    return chosen


def build_record(args, ref):
    now = datetime.datetime.now(datetime.timezone.utc)
    record = {
        "ref": ref,
        "source": args.source,
        "timestamp": now.strftime("%Y-%m-%dT%H:%M:%SZ"),
        "mode": args.mode,
        "stage": args.stage,
        "agent": args.agent,
        "parent_task": args.parent_task,
        "related_ids": split_csv(args.related),
        "human_decision": args.decision,
        "outcome": args.outcome,
        "human_changes": split_csv(args.changes),
        "action_verification": args.verification,
        "limitation": args.limitation,
        "commit": args.commit,
    }
    if args.manual_entry:
        record["id"] = ref
        record["event"] = "manual"
    return record


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument("--ref", help="id of the log entry, or 'latest'")
    parser.add_argument("--decision", required=True)
    parser.add_argument("--outcome", choices=["success", "partial", "rework"], required=True)
    parser.add_argument("--mode", choices=["ask", "plan", "agent", "edit"], default=None)
    parser.add_argument("--stage", default=None)
    parser.add_argument("--changes", default="", help="comma-separated files you changed")
    parser.add_argument("--verification", default="")
    parser.add_argument("--limitation", default=None)
    parser.add_argument("--commit", default=None)
    parser.add_argument("--source", default="human-annotated")
    parser.add_argument("--related", default="", help="comma-separated related ids")
    parser.add_argument("--parent-task", default=None)
    parser.add_argument("--agent", default=None)
    parser.add_argument(
        "--manual-entry",
        action="store_true",
        help="write a standalone entry and do not require --ref",
    )
    return parser.parse_args()


def main():
    args = parse_args()
    if args.manual_entry:
        now = datetime.datetime.now(datetime.timezone.utc)
        ref = f"{now:%Y%m%dT%H%M%SZ}-manual"
    elif not args.ref:
        raise SystemExit("--ref is required unless --manual-entry is set")
    elif args.ref == "latest":
        ref = latest_interaction_id(INTERACTIONS)
    else:
        ref = args.ref

    record = build_record(args, ref)
    LOG_DIR.mkdir(parents=True, exist_ok=True)
    with ANNOTATIONS.open("a", encoding="utf-8") as handle:
        handle.write(json.dumps(record, ensure_ascii=False) + "\n")
    print("annotated", ref)


if __name__ == "__main__":
    main()

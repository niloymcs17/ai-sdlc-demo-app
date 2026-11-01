# Hook: validate_review.py

Event: `stop`  
Command: `python .cursor/hooks/validate_review.py`

The hook reads `transcript_path` as UTF-8 and judges only the last assistant message (`role` / `message.content[].text`). An older findings table in the same file does not hide a later bad review. If the transcript already contains `Reformat your review`, it does not send another follow-up.

## Before

Last assistant message:

```
REVIEW-FINDINGS:
- Finding one: limit check is in the controller
- Finding two: missing test for the boundary
```

Hook stdout:

```json
{"followup_message": "Reformat your review using the review-diff skill table: | # | Finding | Severity (high/med/low) | Location (file:line) | Suggested fix | Verdict | Reason |"}
```

## After

The follow-up asks for the findings table. The same transcript with that table as the last assistant message produces no stdout:

```
REVIEW-FINDINGS:
| # | Finding | Severity (high/med/low) | Location (file:line) | Suggested fix | Verdict | Reason |
| 1 | Limit check is in the controller | high | ApprovalsController.cs:12 | Move the rule into ApprovalService | fix | Business rules belong in Domain |
| 2 | Boundary test is missing | med | ApprovalServiceTests.cs:40 | Add a test at the limit | fix | The rule has no failing test |
```

A second run, after `Reformat your review` was already in the transcript, also produced no follow-up.

The real session transcript (no `REVIEW-FINDINGS:` line) produced no follow-up.

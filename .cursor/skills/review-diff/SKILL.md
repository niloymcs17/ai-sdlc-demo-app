---
name: review-diff
description: >-
  Reviews a diff, commit, or pull request and reports findings in one fixed
  table. Use when the user asks to review a diff, review a commit, run git
  show, review a PR, or look for problems in a change.
---

Review against the AGENTS.md "Review expectations".

Begin the output with the line `REVIEW-FINDINGS:` then output ONLY this table, one row per finding:

| # | Finding | Severity (high/med/low) | Location (file:line) | Suggested fix | Verdict | Reason |

Leave Verdict and Reason as `PENDING`; the human fills them in.
If no findings, output one row "No findings" and list what was checked.

State that these checks were made, in this order:

1. Boundaries 50,000 / 50,000.01 / 500,000 / 500,000.01 use `<=` against `ApprovalLimits` constants.
2. Every refusal path runs before any assignment to `invoice` (status, `DecidedBy`, `DecidedAt`, `Approvals`).
3. A refusal never sets `InvoiceStatus.Rejected`.
4. Same-user check compares `UserId` with ordinal comparison, not references or display names.
5. Second approver must be a Director; check is independent of the same-user check.
6. No limit numbers or role rules in `Controllers/`, `Program.cs` or `wwwroot/app.js`.
7. Tests assert reason and unchanged state; no test mirrors the implementation.
8. Each commit builds and passes tests on its own.

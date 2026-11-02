# Review verdicts (task 18)

Reviewed the table in `docs/evidence/ai-review-raw.md` against the code on `feature/approval-limits-by-role`. Each finding has one verdict.

| # | Finding | Severity | Location | Verdict | Reason / commit |
|---|---|---|---|---|---|
| 1 | The invoices table has no Approvals cell, so the end-to-end test cannot see `None` after a refused approval. | high | `e2e/tests/over-limit-approval.spec.ts:21`, `wwwroot/app.js` `renderInvoices` | **Fixed** | Commit `2df75f7`. The table header and the cell are rendered. Empty `approvals` is exactly `None`; otherwise `userId (role)`. No limit numbers in `app.js`. `dotnet test`: 38 passed. Playwright: 1 passed against that build. |
| 2 | Overlapping `Approve` calls can both see `Approvals.Count == 0` and both append, so a double-click or two Managers can mark an over-Director-limit invoice `Approved`. | high | `ApprovalService.cs:42` and `InMemoryInvoiceRepository.GetById` | **Deferred** | The claim is true: `GetById` returns the live object and drops the lock before the checks and `Approvals.Add`. Not fixed here. The demo store is in-memory, and the right fix is optimistic concurrency on `Update`, not a button disable and not a lock inside the domain service. Write-up: `docs/review/deferred-concurrency.md`. `gh issue create --repo niloymcs17/ai-sdlc-demo-app` did not run to completion: GitHub CLI is not logged in, and connecting GitHub was skipped, so there is no issue number yet. |

Fixed 1, dismissed 0, deferred 1.

## What was checked before the verdicts

Finding 1. At review time `index.html` headers were ID, Customer, Amount, Status, Actions. `renderInvoices` appended the status cell and then the actions cell. `InvoiceAppPage.noApprovalsCell` asks for a cell named `None`. That cell did not exist, so the assertion could not observe an empty approver list. The suggested alternative (drop the locator and read `GET /api/invoices`) was not taken. The test is the UI proof that the list stayed empty, so the cell was added.

Finding 2. `GetById` locks only around `FirstOrDefault` and returns that instance. `Approve` then calls `EnsureCanApprove` and `Approvals.Add` with no lock. The same-user loop and the Director check are inside `if (invoice.Approvals.Count == 0) return`, so they do not run for a call that still sees an empty list. Two such calls can both append. `Update` only swaps the list slot under the lock; it does not re-check the rules.

Sub-agent hooks are still wired: `.cursor/hooks.json` runs `log_event.py` on `subagentStart` and `subagentStop`. Those events are present in `.ai-log/ai-interactions.jsonl` from task 09.

Each commit on `main..HEAD` was tested on its own in a separate worktree (`dotnet test backend/InvoiceApp.sln`). All 14 passed. History was not rewritten: the branch was already published, and `git rebase -x` would have required a force push.

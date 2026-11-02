# Mutation survivors, BEFORE

Measured on tag `mutation-before` (`ea8b7e9`), the agent's first-draft tests. No tests were changed for this run.

Stryker.NET 4.13.0. Config is `backend/tests/InvoiceApp.Tests/stryker-config.json`. The mutate list is the approval files only (`ApprovalService`, `ApprovalLimits`, `Approver`, `ApprovalRecord`, `ApprovalRefusedException`). `DiscountCalculator.cs` is excluded: it has no tests, and the guide's `**/Domain/*.cs` pattern would have counted its mutants as unrelated survivors. Those mutants show up in the report as ignored (22), not in the score.

**Mutation score: 84.75%** (killed 50 / survived 9 / no coverage 0 / timeout 0).

Run time: 00:01:03. Elapsed time from the Stryker log: `00:01:02.7193510`.

No mutant was `NoCoverage`. Every survivor is in `ApprovalService.cs`.

## Survivors

| # | File:line | Mutator | Original | Mutated | Why it survived | Test group that will kill it |
|---|---|---|---|---|---|---|
| S1 | ApprovalService.cs:25 | String | `$"Invoice {invoiceId} is {invoice.Status}; only drafts can be submitted."` | `$""` | `Submit_RejectsInvoiceThatIsNotADraft` asserts the exception type only, not the message | Reason text |
| S2 | ApprovalService.cs:29 | Statement | `_repository.Update(invoice);` in `Submit` | statement removed | The test reads `Status` on the same object the service mutated. `Update` is not observed | State |
| S3 | ApprovalService.cs:37 | String | `"An approver with a user id is required."` | `""` | `Approve_BlankUserId_ThrowsArgumentException` asserts `ArgumentException` and unchanged state, not the message | Reason text |
| S4 | ApprovalService.cs:53 | Statement | `_repository.Update(invoice);` in `Approve` | statement removed | Approval tests read the live invoice. Nothing asserts that `Update` was called | State |
| S5 | ApprovalService.cs:61 | String | `"A reason is required to reject an invoice."` | `""` | `Reject_RequiresAReason` asserts `ArgumentException` only, not the message | Reason text |
| S6 | ApprovalService.cs:65 | Statement | `EnsurePending(invoice);` in `Reject` | statement removed | Reject tests use a pending invoice, so skipping the pending check still passes | State |
| S7 | ApprovalService.cs:71 | Statement | `_repository.Update(invoice);` in `Reject` | statement removed | `Reject_RecordsReasonAndApprover` reads the live invoice. `Update` is not observed | State |
| S8 | ApprovalService.cs:100 | Statement | `return;` after the at-or-under Director limit checks | statement removed | Every under-limit test has an empty approval list, so the later `Approvals.Count == 0` return still exits. The single-approver path is never distinguished from that second return | Combinations |
| S9 | ApprovalService.cs:134 | String | `$"Invoice {invoiceId} was not found."` | `$""` | `Approve_ThrowsWhenInvoiceDoesNotExist` asserts `KeyNotFoundException` only, not the message | Reason text |

## Groups for task 14

| Group | Survivors | What one prompt should add |
|---|---|---|
| Reason text | S1, S3, S5, S9 | Assert the exception message text for submit-not-draft, blank approver, reject without a reason, and missing invoice |
| State | S2, S4, S6, S7 | Assert the repository `Update` result is what was saved, and that `Reject` on a non-pending invoice throws and changes nothing |
| Combinations | S8 | A pending invoice at or under the Director limit that already has an approval record, then a different Manager approves. With the `return` removed, that call is treated as a second approval and refused |
| Boundaries | none | Exact limits were already killed |
| Identity | none | Ordinal `UserId` comparison was already killed |

## Not counted

Ignored mutants (block filter and mutate filter, including all of `DiscountCalculator.cs`) and compile-error mutants are not survivors. They are not in the 84.75% score.

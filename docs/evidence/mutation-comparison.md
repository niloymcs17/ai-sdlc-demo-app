# Mutation comparison, BEFORE and AFTER

Same config as task 13: `backend/tests/InvoiceApp.Tests/stryker-config.json`. Stryker.NET 4.13.0. The mutate list is still only `ApprovalService`, `ApprovalLimits`, `Approver`, `ApprovalRecord`, and `ApprovalRefusedException`. `DiscountCalculator.cs` stays outside that list (22 ignored mutants). Ignored mutants and compile-error mutants are not in the score.

Both runs created 105 mutants, skipped 46 (5 compile errors, 17 block-already-covered, 24 mutate-filter), and tested 59. No mutant was `NoCoverage` or `Timeout` in either run.

```
BEFORE: Mutation score: 84.75%  (killed 50, survived 9, no coverage 0, timeout 0)
AFTER:  Mutation score: 100.00%  (killed 59, survived 0, no coverage 0, timeout 0)
```

BEFORE was tag `mutation-before` (`ea8b7e9`), elapsed `00:01:02.7193510`. AFTER is the strengthened tests from `5e9fa60`, 38 tests discovered, elapsed `00:00:53.1767817`. The AFTER log is `docs/evidence/stryker-after.txt`. The per-file table is `docs/evidence/stryker-after-report.md`: `ApprovalService.cs` 100.00% (58 killed, 0 survived) and `ApprovalRefusedException.cs` 100.00% (1 killed, 0 survived).

The cleartext table wraps its columns, so the wide "survived" cell includes ignored mutants. The score line and the markdown report are the counts used here.

## Task 13 survivors

Every survivor from `docs/evidence/mutation-survivors-before.md` is killed. The killing test is the one Stryker recorded in `stryker-after-report.html` for that line and mutator. There is no remaining survivor, so none is accepted as equivalent.

| Survivor (task 13) | Status after | Killed by test |
|---|---|---|
| S1 `ApprovalService.cs:25` string `$"Invoice {invoiceId} is {invoice.Status}; only drafts can be submitted."` to `$""` | Killed | `Submit_RejectsInvoiceThatIsNotADraft` |
| S2 `ApprovalService.cs:29` statement `_repository.Update(invoice)` removed in `Submit` | Killed | `Submit_MovesDraftToPendingApproval` |
| S3 `ApprovalService.cs:37` string `"An approver with a user id is required."` to `""` | Killed | `Approve_BlankUserId_ThrowsArgumentException`, `Approve_NullApprover_ThrowsArgumentException` |
| S4 `ApprovalService.cs:53` statement `_repository.Update(invoice)` removed in `Approve` | Killed | `Approve_SetsStatusApproverAndTimestamp` |
| S5 `ApprovalService.cs:61` string `"A reason is required to reject an invoice."` to `""` | Killed | `Reject_RequiresAReason` |
| S6 `ApprovalService.cs:65` statement `EnsurePending(invoice)` removed in `Reject` | Killed | `Reject_InvoiceNotPending_ThrowsAndChangesNothing` (Draft, Approved, and Rejected) |
| S7 `ApprovalService.cs:71` statement `_repository.Update(invoice)` removed in `Reject` | Killed | `Reject_RecordsReasonAndApprover` |
| S8 `ApprovalService.cs:100` statement `return` removed after the at-or-under Director limit checks | Killed | `Approve_ManagerOnAlreadyApprovedInvoiceAtManagerLimit_Approves` |
| S9 `ApprovalService.cs:134` string `$"Invoice {invoiceId} was not found."` to `$""` | Killed | `Approve_ThrowsWhenInvoiceDoesNotExist` |

## Remaining survivors

None. Nothing is left as an equivalent mutant, and nothing is a dead-code gap to carry into the task 18 review.

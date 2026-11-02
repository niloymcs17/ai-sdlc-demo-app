# Test review notes (task 14)

Reviewed `backend/tests/InvoiceApp.Tests/ApprovalServiceTests.cs` as it stood after task 13, before the survivor-driven edits. Production code was not changed. Survivors are the nine rows in `docs/evidence/mutation-survivors-before.md`.

`Fails if <= became <` means: this test fails if the exact limit stops being accepted (the boundary comparison rejects the ceiling).

## Verdict per test

| Test | Rule / edge | Outcome asserted | Reason text asserted | Status unchanged | Approvals unchanged | Fails if `<=` became `<`? | Duplicate / mirrors impl? | Verdict | Why |
|---|---|---|---|---|---|---|---|---|---|
| `Submit_MovesDraftToPendingApproval` | Submit draft | yes (status only) | n/a | n/a | n/a | n/a | no | Modify | Reads `Status` on the live object. `FakeInvoiceRepository.Update` is a no-op, so S2 (removing `Update` in `Submit`) survives. |
| `Submit_RejectsInvoiceThatIsNotADraft` | Submit non-draft | type only | **no** | **no** | **no** | n/a | no | Modify | `Assert.Throws<InvalidOperationException>` only. S1 empties the message and still passes. Status is not snapshotted. |
| `Approve_SetsStatusApproverAndTimestamp` | Single approve, amount 100, Manager | yes | n/a | n/a | **no** | no (amount is 100, not a ceiling) | overlaps the 100 row of `Approve_ManagerWithinLimit_ApprovesInvoice`, not the same case (user id `demo.approver`) | Modify | Does not assert the approval record or that `Update` ran (S4). Keep the test; it is not a true duplicate. |
| `Approve_RejectsInvoiceThatIsStillADraft` | Approve draft | type only | **no** | **no** | **no** | n/a | **yes** — Draft row of `Approve_InvoiceNotPending_ThrowsStatusErrorRatherThanRefusal` | Delete | Same call, same Draft status, weaker assertions. The theory already checks the message and that status stays Draft. |
| `Approve_ThrowsWhenInvoiceDoesNotExist` | Missing invoice | type only | **no** | n/a | n/a | n/a | no | Modify | `KeyNotFoundException` only. S9 empties `Invoice {id} was not found.` |
| `Reject_RecordsReasonAndApprover` | Reject pending | status and note | n/a (success) | n/a | n/a | n/a | no | Modify | Name says approver; `DecidedBy` and `DecidedAt` are not asserted. Live-object read lets S7 (removing `Update` in `Reject`) survive. |
| `Reject_RequiresAReason` | Blank reject reason | type only | **no** | **no** | **no** | n/a | no | Modify | Whitespace reason throws `ArgumentException` with no message check. S5 survives. Invoice state is not snapshotted. |
| `Approve_ManagerWithinLimit_ApprovesInvoice` | R1 / E1 and well below | yes | n/a | n/a | no record asserted | **yes** (member `ApprovalLimits.ManagerLimit`) | no | Keep | Exact Manager ceiling and 100 both expect `Approved`. The ceiling row fails if that amount is no longer approved. |
| `Approve_ManagerAboveLimit_RefusesAndLeavesInvoiceUnchanged` | R1 / E2 | yes | yes (code and message) | yes | **no** (`AssertUnchanged` omits `Approvals`) | no (already over the ceiling) | no | Modify | Reason text is solid. The shared snapshot does not copy `Approvals`, so an appended record would not fail this test. |
| `Approve_DirectorWithinLimit_ApprovesInvoice` | R2 / E3 and E13 | yes | n/a | n/a | no record asserted | **yes** (member `ApprovalLimits.DirectorLimit`) | no | Keep | Exact Director ceiling, and Manager ceiling + 1 (50,001) for a Director. |
| `Approve_DirectorAloneAboveDirectorLimit_StaysPendingWithOneRecord` | R3 / E4 | yes | n/a | stays pending, decided fields null | records the one approval | no (already over) | no | Modify | Covers Director limit + 0.01 only. The matrix also asks for 500,001 left unapproved. Add that amount; do not drop 0.01. |
| `Approve_TwoDirectorsAboveDirectorLimit_ApprovesWithRecordsInOrder` | R3 / E7 | yes | n/a | n/a | order asserted | no | no | Keep | Two different Directors, records in order, `DecidedBy` is the second. |
| `Approve_SecondApproverIsManager_RefusesAndLeavesApprovalsUnchanged` | R3 / E5 | yes | yes | yes | yes (`.ToList()`) | no | no | Keep | Director then a different Manager. Snapshot is a copy, then the list is compared. |
| `Approve_SameDirectorApprovesTwice_RefusesAndLeavesOneRecord` | R4 / E6 | yes | yes | yes | yes (`.ToList()`) | no | no | Keep | Same Director id twice. One record remains. |
| `Approve_SameUserIdOnDistinctApproverObjects_RefusesAsSameUser` | R4 / E11 | yes | yes | yes | yes (`.ToList()`) | no | no | Keep | Two objects, same `UserId`. This is the identity case, not a duplicate of the same-variable case. |
| `Approve_SameManagerApprovesTwice_ReportsSameUserBeforeDirectorCheck` | R4 / D10 | yes | yes, and not the Director code | yes | yes (`.ToList()`) | no | no | Keep | Both checks fail; same-user reason is the one returned. |
| `Approve_TwoManagersAboveDirectorLimit_RefusesBecauseSecondMustBeDirector` | R3 / E16 | yes | yes | yes | yes (`.ToList()`) | no | no | Keep | Different Managers. Second is refused; first record remains. |
| `Approve_ManagerFirstThenDirectorAboveDirectorLimit_ApprovesOnSecond` | R3 / E9 | yes | n/a | n/a | both records asserted | no | no | Keep | Manager first on Director limit + 1 (500,001), then a Director. Status stays pending after the first. |
| `Approve_AmountNotPositive_RefusesAndLeavesInvoiceUnchanged` | E10 zero and -1, Manager | yes | yes | yes | **no** (helper omits `Approvals`) | n/a | no | Modify | Reason text is right. Unchanged check is incomplete. |
| `Approve_NegativeAmountByDirector_RefusesAndLeavesInvoiceUnchanged` | E10 Director | yes | yes | yes | **no** (helper) | n/a | no | Modify | Not a duplicate of the Manager theory: the role is Director. Same snapshot hole. |
| `Approve_RoleWithoutAuthority_RefusesAndLeavesInvoiceUnchanged` | E12 | yes | yes | yes | **no** (helper) | n/a | no | Keep | Approver, Admin, `Clerk`, and empty role. Reason text is exact. Snapshot hole is fixed in the shared helper, not by rewriting this test. |
| `Approve_ManagerAtDirectorLimit_RefusesAndLeavesInvoiceUnchanged` | Manager at Director ceiling | yes | yes | yes | **no** (helper) | **yes** for the Director-ceiling check in `EnsureCanApprove` | no | Keep | 500,000 is inside the Director path and over the Manager path, so it must be refused. Helper fix covers `Approvals`. |
| `Approve_BlankUserId_ThrowsArgumentException` | Blank user id | type only | **no** | yes | **no** (helper) | n/a | no | Modify | S3. Message `An approver with a user id is required.` is not asserted. |
| `Approve_InvoiceNotPending_ThrowsStatusErrorRatherThanRefusal` | E15 | yes | yes | status only | **no** | n/a | no | Modify | Message is exact and the type is not `ApprovalRefusedException`. `DecidedBy`, `DecidedAt`, and `Approvals` are not snapshotted. |

Names follow `Method_Condition_ExpectedResult`. No test recomputes the decision with the same `if` as `ApprovalService`.

## Minimum matrix

| Rule | Required tests | Present? |
|---|---|---|
| R1 | Manager 50,000 approved; Manager 50,001 refused with reason; Manager well below approved | Yes. `Approve_ManagerWithinLimit_ApprovesInvoice` includes `ApprovalLimits.ManagerLimit` and 100. `Approve_ManagerAboveLimit_RefusesAndLeavesInvoiceUnchanged` adds 0.01 and 1 to that limit and asserts code `exceeds-manager-limit` and message `Amount exceeds the Manager approval limit.` |
| R2 | Director 500,000 approved; Director 500,001 alone not finally approved; Director 50,001 approved | Yes after this task. Exact Director limit and Manager limit + 1 are in `Approve_DirectorWithinLimit_ApprovesInvoice`. Alone-above was only Director limit + 0.01; the theory now also includes Director limit + 1. |
| R3 | Two Directors above 500,000 approved; Manager as second approver refused; Director then Manager refused | Yes. `Approve_TwoDirectorsAboveDirectorLimit_ApprovesWithRecordsInOrder`, `Approve_TwoManagersAboveDirectorLimit_RefusesBecauseSecondMustBeDirector`, `Approve_SecondApproverIsManager_RefusesAndLeavesApprovalsUnchanged`. |
| R4 | Same user twice refused (same ID, distinct objects); two different users with same role accepted | Yes. `Approve_SameUserIdOnDistinctApproverObjects_RefusesAsSameUser`. Two Directors above the Director limit are accepted. |
| R5 | After every refusal above: status and approver list identical to before | Yes after this task. Two-approver refusals already copied `Approvals` with `.ToList()`. `Capture` now copies `Approvals` and `DecisionNote`, and `AssertUnchanged` compares those copies. |
| Extra | Zero/negative amount; null approver; unknown role | Yes after this task. Zero and negative are covered for a Manager; negative is also covered for a Director. Unknown role is covered. Null approver is `Approve_NullApprover_ThrowsArgumentException` (D16). |

### Null approver decision

A null approver is rejected before the invoice is loaded. Decided behaviour for this task: throw `ArgumentException` with message `An approver with a user id is required.` and parameter name `approver`, and leave the invoice unchanged. `ArgumentNullException` was the recommendation. It was not adopted. `ArgumentNullException` is a subclass of `ArgumentException`, and `ApiControllerBase.Handle` already maps `ArgumentException` to 400, so the HTTP result is the same. Splitting the two cases means editing `ApprovalService`, and this task does not change production code. The new test locks the message, the exact exception type, and an unchanged snapshot. That also kills S3.

## What the generated tests got wrong

1. **Exception type only.** `Submit_RejectsInvoiceThatIsNotADraft`, `Approve_ThrowsWhenInvoiceDoesNotExist`, `Reject_RequiresAReason`, and `Approve_BlankUserId_ThrowsArgumentException` never read `Message`. Stryker emptied those four strings (S1, S9, S5, S3) and every test stayed green. Each of those tests now asserts the exact message. The submit and reject cases also snapshot status, `DecidedBy`, `DecidedAt`, and `Approvals`.

2. **`AssertUnchanged` did not look at the approval list.** `Capture` stored `Status`, `DecidedBy`, and `DecidedAt` only. A refusal that appended an `ApprovalRecord` before throwing would still pass `Approve_ManagerAboveLimit_RefusesAndLeavesInvoiceUnchanged`, both amount tests, the role test, the blank-user-id test, and `Approve_ManagerAtDirectorLimit_RefusesAndLeavesInvoiceUnchanged`. The two-approver tests did copy the list with `.ToList()`; the shared helper did not. `Capture` now copies `Approvals` and `DecisionNote`, and `AssertUnchanged` compares those copies. It does not compare the list to itself.

3. **Saved state was the live object, and `Update` was invisible.** `FakeInvoiceRepository.Update` discarded its argument. Submit, approve, and reject tests then read `invoice.Status` on the instance the service had already mutated. Removing `_repository.Update(invoice)` (S2, S4, S7) changed nothing those tests could see. The fake now keeps every invoice passed to `Update`. The success tests assert that the saved instance is the one that was returned.

4. **The early return on the single-approver path was never isolated (S8).** Every under-limit test started with an empty `Approvals` list. Deleting `return` after the Director-limit checks still hit `if (invoice.Approvals.Count == 0) return`, so the mutant lived. A pending invoice at the Manager limit that already has another user's approval must still be approved by a different Manager. With the `return` gone, that Manager is treated as a second approver and refused.

5. **Reject never saw a non-pending invoice (S6).** `EnsurePending` in `Reject` could be deleted because every reject test used `PendingApproval`. A Draft, Approved, or Rejected invoice must throw `Invoice {id} is {status}; only pending invoices can be decided.` and stay as it was.

6. **A weaker duplicate.** `Approve_RejectsInvoiceThatIsStillADraft` repeats the Draft row of `Approve_InvoiceNotPending_ThrowsStatusErrorRatherThanRefusal` and checks less. It is the one deletion. The theory remains.

7. **`Reject_RecordsReasonAndApprover` did not record the approver.** It asserted `Rejected` and `DecisionNote` only. It now also asserts `DecidedBy`, `DecidedAt`, and the repository save.

Boundary operators were already killed: exact ceilings, ceiling + 0.01, and ceiling + 1 are in the theories. No boundary survivor was open. Identity comparison was already killed by the distinct-object test.

## Survivor prompts and follow-up edits

Filled in as each group lands. `human_changes` are edits made after reading the agent's diff.

| Group | Related | Prompt outcome | Human changes |
|---|---|---|---|
| Reason text | S1, S3, S5, S9 | Accepted. Exact messages, including the `ArgumentException` parameter suffix. `Capture` copies `Approvals` and `DecisionNote`. Null approver test added. 34 passed. | None on that diff. Later, failure paths also assert `Updated` is empty. |
| State | S2, S4, S6, S7 | Accepted. `FakeInvoiceRepository.Update` records the same instance. Submit, approve, and reject success tests require that save. Non-pending reject throws the status message and changes nothing. 37 passed. | None on that diff. |
| Combinations | S8 | Accepted. A pending invoice at the Manager limit that already has another user's approval is still approved by a different Manager. Director-alone theory adds Director limit + 1 and keeps + 0.01. 39 passed. | None on that diff. |

### Human edits after the three prompts

- Deleted `Approve_RejectsInvoiceThatIsStillADraft`. It was the Draft row of `Approve_InvoiceNotPending_ThrowsStatusErrorRatherThanRefusal` with no message and no snapshot.
- `Submit_RejectsInvoiceThatIsNotADraft`, `Reject_RequiresAReason`, blank user id, and null approver now also assert the repository `Updated` list is empty.
- `Approve_InvoiceNotPending_ThrowsStatusErrorRatherThanRefusal` now snapshots `DecidedBy`, `DecidedAt`, `DecisionNote`, and `Approvals`, and asserts `Update` was not called. The message and status assertions are unchanged.
- D16 / E17 in the OpenSpec change: null and blank user id throw `ArgumentException` with `An approver with a user id is required.` `ArgumentNullException` was not adopted. It would still be 400, and this task does not edit `ApprovalService`.

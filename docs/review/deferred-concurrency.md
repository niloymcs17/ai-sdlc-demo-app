## Problem

`InMemoryInvoiceRepository.GetById` returns the live invoice and releases its lock before `ApprovalService.Approve` runs `EnsureCanApprove`, appends an `ApprovalRecord`, and calls `Update`.

Two overlapping approvals can both observe `Approvals.Count == 0`. Both then append. For an amount above `ApprovalLimits.DirectorLimit`, `Approve` sets `Approved` when the count reaches 2. That skips the same-user check and the "second approver must be a Director" check, which only run when the count is already non-zero at the start of the call.

A double-click on Approve can record the same user twice. Two Managers can finish the same invoice the same way. `List<T>.Add` is also not safe for concurrent mutation of that shared list.

## Why this is not fixed on the feature branch

The store is an in-memory demo. A correct fix is optimistic concurrency: `Update` must reject a write when the approval version is not the version that was read, and the service must run the rules again on the winning version. Disabling the Approve button only covers one browser tab. Putting a lock inside `ApprovalService` would also pull storage concerns into the domain project.

## Expected behaviour

The second overlapping call is refused, or it re-reads and then applies R3 and R4. The invoice never becomes `Approved` with two records from the same user, or with two Managers, solely because the calls overlapped.

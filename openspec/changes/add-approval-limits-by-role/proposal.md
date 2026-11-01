# Proposal

## Why

Approval today does not depend on the sign-in role or the invoice amount, so a Manager and a Director are treated the same and a very large invoice can be approved by one person. The rules also have ambiguous corners (who may be first above the Director ceiling, zero amounts, refusal versus rejection, and identity by user id) that have to be decided before implementation.

## What Changes

- A Manager may approve a pending invoice up to the inclusive Manager limit. A Director may approve up to the inclusive Director limit in one approval.
- An amount above the Director limit needs two distinct approvers. The second must be a Director. The same user never counts twice.
- A refused attempt returns a clear reason and changes nothing. Status stays `PendingApproval`. Only the explicit Reject action sets `Rejected`.
- **BREAKING:** `Approve` takes an `Approver` (user id and role) instead of a bare user name, so a role-less call cannot bypass the limits.
- Limits exist only as `ApprovalLimits.ManagerLimit` and `ApprovalLimits.DirectorLimit`. Money is `decimal`. Users are compared by `UserId` with `StringComparison.Ordinal`.

## Capabilities

### New Capabilities

- `approval-limits`: Inclusive role ceilings, the two-approver path above the Director ceiling, and refusal that leaves the invoice unchanged.

### Modified Capabilities

- None. There is no existing OpenSpec capability for approval.

## Impact

- Domain: `ApprovalLimits`, `Roles`, `Approver`, `ApprovalRefusedException`, `ApprovalService`, and in Phase 2 `ApprovalRecord` plus `Invoice.Approvals`.
- API: `ApprovalsController` maps the session to an `Approver` and extends `RequireRole`. No approval rules and no limit numbers in the controller or `Program.cs`.
- Demo data: `appsettings.json` users and pending seed invoices in `InMemoryInvoiceRepository`.
- Tests: `ApprovalServiceTests` for Phase 1, then Phase 2. The UI does not gain limit numbers.

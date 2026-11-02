# Design

## Context

See proposal.md for why. `ApprovalService` already moves invoices through Draft, PendingApproval, Approved, and Rejected. Repositories return live references, so a field write before a thrown refusal is already visible to the caller. Controllers must only map the session to an `Approver` and call the service.

## Goals / Non-Goals

**Goals:**

- Encode R1 to R5 in the domain, with inclusive ceilings and validate-before-mutate.
- Keep Phase 1 (single approver: R1, R2, R5) independently deliverable from Phase 2 (two approvers: R3, R4, R5).
- Give every refusal a stable reason code and message, and leave the invoice `PendingApproval`.

**Non-Goals:**

- Showing limit numbers in `wwwroot/app.js`.
- A new invoice status for "waiting for the second approver".
- Mapping the existing Approver role onto Manager, or Admin onto Director.
- Putting approval rules or limit numbers in controllers or `Program.cs`.

## Decisions

| ID | Chosen option | Reason |
|---|---|---|
| D1 | Add roles `Manager` and `Director`, and users `demo.manager`, `demo.director`, and `demo.director2` (password `ChangeMe123!`) in `appsettings.json`. | New approval roles need their own demo users so the existing Approver and Admin accounts stay available. |
| D2 | Only Manager and Director have approval authority. Any other role is refused with `Only a Manager or a Director can approve an invoice.` Admin still deletes. `RequireRole` on approvals is Manager, Director, Approver, and Admin. | The domain must explain the refusal; a bare 403 would hide the reason. |
| D3 | Username is the user id. Compare with `StringComparison.Ordinal`. Domain type `Approver(string UserId, string Role)`. | Login is case-sensitive, so identity is the user id, not the display name or the object reference. |
| D4 | A refused approval leaves the invoice `PendingApproval`. Only the explicit Reject action sets `Rejected`. | A refused attempt is not a decision to decline the invoice. |
| D5 | Throw `ApprovalRefusedException : InvalidOperationException` with a reason code and a clear message. `Handle` maps that to 409. | The existing error pipeline already shows `InvalidOperationException` messages as 409. |
| D6 | One Domain constant per reason. Codes and messages: `exceeds-manager-limit` / `Amount exceeds the Manager approval limit.`; `role-not-permitted` / `Only a Manager or a Director can approve an invoice.`; `same-approver` / `The same user cannot approve this invoice twice.`; `second-approver-must-be-director` / `The second approver must be a Director.`; `amount-not-positive` / `Invoice amount must be greater than zero.`; Phase 1 only `requires-two-approvers` / `Invoices above 500,000.00 require two approvers.` | Tests and the UI need one stable string per reason. The first four match the domain constants; the last two are stated here because the domain does not define them yet. |
| D7 | A Manager MAY be the first approver above the Director limit. Status stays `PendingApproval` and the approval is recorded. Human decision for E9. | The brief constrains only the second approver, and a Director must still sign off. |
| D8 | Zero or negative amount is refused because the amount must be greater than zero. Message: `Invoice amount must be greater than zero.` Human decision for E10. | Approval checks the amount even though create already blocks it. |
| D9 | After the first of two approvals, status stays `PendingApproval`. `Invoice.Approvals` records `ApprovalRecord(UserId, Role, ApprovedAt)`. Final approval sets `Approved`, `DecidedBy`, and `DecidedAt`. Single approvals also append a record. | The invoice list already shows `PendingApproval`, so a new status is unnecessary. |
| D10 | When both second-approval checks fail, report the same-user reason first, then the Director check. | The same-user failure is the more specific identity error, and each check is also tested alone. |
| D11 | `ApprovalLimits.ManagerLimit` and `ApprovalLimits.DirectorLimit` in `backend/src/InvoiceApp.Domain/ApprovalLimits.cs` are the only copies of the limits. No limit literals in controllers, `Program.cs`, tests' production paths, or `app.js`. | A second literal would let the ceilings drift apart. |
| D12 | Change only the arguments of `Approve_SetsStatusApproverAndTimestamp` to `new Approver("demo.manager", Roles.Manager)` and keep every assertion. | The new signature must carry a role, and existing assertions stay as they are. |
| D13 | Phase 1 refuses amounts above the Director limit with `Invoices above 500,000.00 require two approvers.` Phase 2 supersedes that placeholder. The Phase 2 commit body says so. | Phase 1 still needs a defined result before the two-approver path exists. |
| D14 | Reject after one of two approvals stays allowed for Manager or Director. Existing reject behaviour is unchanged and `Approvals` is kept for audit. | Declining an invoice is a different action from refusing an approval attempt. |
| D15 | Use OpenSpec. | It is the spec tool chosen for this repo and it generates Cursor slash commands. |
| D16 | A null approver and a blank user id both throw `ArgumentException` with `An approver with a user id is required.` `Handle` already maps that to 400. | `ArgumentNullException` would not change the HTTP result, and splitting the guard needs a production edit this test review does not make. |

### Check order

On a first approval at or under the Director limit: amount greater than zero, then role, then that role's limit, then mutate.

On a first approval above the Director limit (Phase 2): amount greater than zero, then role is Manager or Director, then append one `ApprovalRecord` and stay `PendingApproval`. Do not set `DecidedBy` or `DecidedAt`.

On a second approval: same-user check first (D10), then Director check, then set `Approved`, `DecidedBy`, and `DecidedAt`.

Phase 1 stops before the two-approver branch and refuses with the D13 message instead of recording a first approval.

### Layering

- Controllers map the session username and role to `Approver` and call `ApprovalService`. No ceilings and no rule branches.
- `ApprovalService` and `ApprovalLimits` own the rules. No web and no storage code in the domain.
- The repository only loads and saves. Tests use `FakeInvoiceRepository` and snapshot status and approver fields before the call.

## Risks / Trade-offs

- [Phase 1 and Phase 2 disagree on an amount above the Director limit] → D13 is an explicit placeholder. Phase 2 replaces that test and names the replacement in the commit body.
- [A Manager cannot approve 60,000 alone but can be first on a much larger invoice] → Accepted by D7 because the second signature must still be a Director. Recorded so implementers do not "fix" it.
- [Live repository objects show a half-updated invoice if a check throws late] → Validate-before-mutate is mandatory. Tests compare a snapshot taken before the call.

## Migration Plan

1. Phase 1 types, red tests, then `Approve(string, Approver)` for the single-approver path, then API wiring and demo data.
2. Phase 2 adds `ApprovalRecord` and `Invoice.Approvals`, red tests for the two-approver path, then the service behaviour that supersedes D13.
3. No data migration. The store is in-memory.

## Open Questions

None. D1 to D16 are resolved above. E9 is D7 and E10 is D8. Null and blank approvers are D16.

## Edge cases

| # | Scenario | Expected |
|---|---|---|
| E1 | Manager approves exactly 50,000 | Approved |
| E2 | Manager approves 50,000.01 / 50,001 | Refused: exceeds Manager limit; invoice unchanged. Message: `Amount exceeds the Manager approval limit.` |
| E3 | Director approves exactly 500,000 | Approved (single approver) |
| E4 | Director approves 500,000.01 alone | Phase 2: stays `PendingApproval`, one approval recorded, needs a second. Phase 1: refused (D13). Message: `Invoices above 500,000.00 require two approvers.` |
| E5 | Over 500,000: second approver is a Manager | Refused: second approver must be a Director; invoice unchanged. Message: `The second approver must be a Director.` |
| E6 | Over 500,000: same user approves twice | Refused: same user; invoice unchanged. Message: `The same user cannot approve this invoice twice.` |
| E7 | Over 500,000: two different Directors | Approved; `DecidedBy` = second Director |
| E8 | Any refusal | Status, `DecidedBy`, `DecidedAt`, `Approvals` unchanged |
| E9 | Manager is first approver on 500,001 | Accepted, pending (D7). Status stays `PendingApproval` and one approval is recorded |
| E10 | Zero or negative amount | Refused: amount must be greater than zero (D8). Message: `Invoice amount must be greater than zero.` |
| E11 | Two distinct `Approver` objects with the same `UserId` | Treated as the same user (R4). Message: `The same user cannot approve this invoice twice.` |
| E12 | Role `Approver` / `Admin` / unknown | Refused: role has no approval authority (D2). Message: `Only a Manager or a Director can approve an invoice.` |
| E13 | Director approves 50,001 | Approved (Director limit covers it) |
| E14 | Same user who is a Manager approves second (both checks fail) | Same-user reason returned (D10). Message: `The same user cannot approve this invoice twice.` |
| E15 | Approve an invoice that is not `PendingApproval` | Existing 409 behaviour unchanged. Message: `Invoice {id} is {status}; only pending invoices can be decided.` |
| E16 | Two different Managers above 500,000 | Second refused: must be a Director. Message: `The second approver must be a Director.` |
| E17 | Null approver, or a blank user id | `ArgumentException`: `An approver with a user id is required.` Invoice unchanged. HTTP 400 (D16). |

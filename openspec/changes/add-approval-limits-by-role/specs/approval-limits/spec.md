# Spec Delta

## Purpose

Defines who may approve a pending invoice, the inclusive Manager and Director ceilings, the two-approver path above the Director ceiling, and the rule that a refused attempt leaves the invoice unchanged.

## ADDED Requirements

### Requirement: Manager approval limit
A Manager SHALL be able to approve a pending invoice whose amount is greater than zero and less than or equal to the Manager limit in one approval. A greater amount that is still within the Director limit SHALL be refused. Amounts SHALL be `decimal`. The Manager limit SHALL be `ApprovalLimits.ManagerLimit` only.

#### Scenario: E1 Manager approves exactly the Manager limit
- **GIVEN** a pending invoice for exactly 50,000 with no approval recorded
- **WHEN** a Manager approves it
- **THEN** the invoice is `Approved`, `DecidedBy` is that Manager's `UserId`, and `DecidedAt` is set

#### Scenario: E2 Manager approves one cent over the Manager limit
- **GIVEN** a pending invoice for 50,000.01
- **WHEN** a Manager approves it
- **THEN** the attempt throws `ApprovalRefusedException` (an `InvalidOperationException`) with reason code `exceeds-manager-limit` and message `Amount exceeds the Manager approval limit.`
- **AND** status stays `PendingApproval` and `Status`, `DecidedBy`, `DecidedAt`, and `Approvals` are unchanged

#### Scenario: E2 Manager approves 50,001
- **GIVEN** a pending invoice for 50,001
- **WHEN** a Manager approves it
- **THEN** the attempt throws `ApprovalRefusedException` with reason code `exceeds-manager-limit` and message `Amount exceeds the Manager approval limit.`
- **AND** the invoice is not `Rejected` and nothing on the invoice changes

### Requirement: Director single approval limit
A Director SHALL be able to approve a pending invoice whose amount is greater than zero and less than or equal to the Director limit in one approval. The Director limit SHALL be `ApprovalLimits.DirectorLimit` only. Both 50,000 and 500,000 are inside the role that is allowed to approve them.

#### Scenario: E3 Director approves exactly the Director limit
- **GIVEN** a pending invoice for exactly 500,000
- **WHEN** a Director approves it
- **THEN** the invoice is `Approved` by that single approver, `DecidedBy` is that Director's `UserId`, and `DecidedAt` is set

#### Scenario: E13 Director approves 50,001
- **GIVEN** a pending invoice for 50,001
- **WHEN** a Director approves it
- **THEN** the invoice is `Approved` because the Director limit covers it

### Requirement: Two approvers above the Director limit
An amount greater than the Director limit SHALL NOT be finally approved by one person. After the first valid approval the status SHALL stay `PendingApproval`, the approver SHALL be recorded, and `DecidedBy` and `DecidedAt` SHALL stay unset. Final approval SHALL set `Approved`, `DecidedBy` to the second approver's `UserId`, and `DecidedAt`. The second approver SHALL be a Director. This is the Phase 2 behaviour and it supersedes the Phase 1 refusal.

#### Scenario: E4 Phase 2 Director approves 500,000.01 alone
- **GIVEN** a pending invoice for 500,000.01 with no approval recorded
- **WHEN** a Director approves it
- **THEN** status stays `PendingApproval`, one approval is recorded, and a second approver is still required

#### Scenario: E9 Manager is first approver on 500,001
- **GIVEN** a pending invoice for 500,001
- **WHEN** a Manager approves it as the first approver
- **THEN** the attempt is accepted, status stays `PendingApproval`, and one approval is recorded for that Manager

#### Scenario: E7 two different Directors
- **GIVEN** a pending invoice above the Director limit that a Director has already approved
- **WHEN** a different Director approves it
- **THEN** the invoice is `Approved` and `DecidedBy` is the second Director's `UserId`

#### Scenario: E5 second approver is a Manager
- **GIVEN** a pending invoice above the Director limit that already has one approval from someone else
- **WHEN** a different Manager approves it as the second approver
- **THEN** the attempt throws `ApprovalRefusedException` with reason code `second-approver-must-be-director` and message `The second approver must be a Director.`
- **AND** status, `DecidedBy`, `DecidedAt`, and `Approvals` stay as they were after the first approval

#### Scenario: E16 two different Managers above the Director limit
- **GIVEN** a pending invoice above the Director limit that a Manager has already approved
- **WHEN** a different Manager approves it
- **THEN** the second attempt throws `ApprovalRefusedException` with reason code `second-approver-must-be-director` and message `The second approver must be a Director.`
- **AND** the first approval remains and status stays `PendingApproval`

### Requirement: Same user cannot approve twice
The same user SHALL never count as both approvers on one invoice. Two `Approver` values SHALL be the same user when their `UserId` values are equal under `StringComparison.Ordinal`, even when they are different objects. Comparison SHALL NOT use display name or object reference.

#### Scenario: E6 same user approves twice
- **GIVEN** a pending invoice above the Director limit that this user has already approved
- **WHEN** the same user approves it again
- **THEN** the attempt throws `ApprovalRefusedException` with reason code `same-approver` and message `The same user cannot approve this invoice twice.`
- **AND** the invoice is unchanged by that second attempt

#### Scenario: E11 two Approver objects with the same UserId
- **GIVEN** a pending invoice above the Director limit already approved by user id `demo.director`
- **WHEN** a different `Approver` object with `UserId` `demo.director` approves it
- **THEN** the attempt is refused as the same user with message `The same user cannot approve this invoice twice.`

#### Scenario: E14 both the same-user check and the Director check fail
- **GIVEN** a pending invoice above the Director limit whose first approver is a Manager
- **WHEN** that same Manager approves it again
- **THEN** the same-user reason is returned first, message `The same user cannot approve this invoice twice.`, and the Director check is not the reported reason

### Requirement: Refused approval changes nothing
A refused approval SHALL return a clear reason and SHALL change nothing on the invoice. It SHALL NOT be a rejection: status SHALL stay `PendingApproval`. Only the explicit Reject action SHALL set `Rejected`. Every check SHALL finish before the first assignment to the invoice.

#### Scenario: E8 any refusal
- **GIVEN** a pending invoice and a snapshot of `Status`, `DecidedBy`, `DecidedAt`, and `Approvals`
- **WHEN** an approval attempt is refused
- **THEN** those four fields match the snapshot and the status is not `Rejected`

#### Scenario: explicit Reject sets Rejected
- **GIVEN** a pending invoice
- **WHEN** an authorized user rejects it with a reason
- **THEN** status is `Rejected`
- **AND** a refused approval of that same invoice would have left it `PendingApproval`

### Requirement: Amount must be greater than zero
A zero or negative amount SHALL be refused at approval time, including when the caller is a Manager or a Director.

#### Scenario: E10 zero amount
- **GIVEN** a pending invoice for amount 0
- **WHEN** a Manager or a Director approves it
- **THEN** the attempt throws `ApprovalRefusedException` with reason code `amount-not-positive` and message `Invoice amount must be greater than zero.`
- **AND** `Status`, `DecidedBy`, `DecidedAt`, and `Approvals` are unchanged

#### Scenario: E10 negative amount
- **GIVEN** a pending invoice for a negative amount
- **WHEN** a Director approves it
- **THEN** the attempt throws `ApprovalRefusedException` with message `Invoice amount must be greater than zero.`
- **AND** the invoice stays `PendingApproval`

### Requirement: Only Manager and Director can approve
Any role other than Manager or Director SHALL be refused. Admin SHALL still be able to delete invoices. The approvals endpoint SHALL allow Manager, Director, Approver, and Admin through role checks so the domain, not a bare 403, explains the refusal.

#### Scenario: E12 role Approver, Admin, or unknown
- **GIVEN** a pending invoice
- **WHEN** a user whose role is `Approver`, `Admin`, or any other role approves it
- **THEN** the attempt throws `ApprovalRefusedException` with reason code `role-not-permitted` and message `Only a Manager or a Director can approve an invoice.`
- **AND** the invoice stays `PendingApproval` and is unchanged

### Requirement: Phase 1 refuses amounts above the Director limit
Until the two-approver flow is delivered, an amount above the Director limit SHALL be refused and the invoice SHALL be unchanged. Phase 2 supersedes this requirement with the pending-second-approval behaviour. The Phase 2 commit SHALL say that this placeholder was replaced.

#### Scenario: E4 Phase 1 Director alone on 500,000.01
- **GIVEN** Phase 1 is in effect and a pending invoice for 500,000.01
- **WHEN** a Director approves it
- **THEN** the attempt throws `ApprovalRefusedException` with reason code `requires-two-approvers` and message `Invoices above 500,000.00 require two approvers.`
- **AND** status stays `PendingApproval` and no approval is recorded

### Requirement: Invoice that is not pending
Approving an invoice that is not `PendingApproval` SHALL keep the existing non-pending error. That error SHALL NOT be an `ApprovalRefusedException`.

#### Scenario: E15 invoice is not PendingApproval
- **GIVEN** an invoice whose status is `Draft`, `Approved`, or `Rejected`
- **WHEN** a Manager or a Director approves it
- **THEN** the attempt throws `InvalidOperationException` with message `Invoice {id} is {status}; only pending invoices can be decided.`
- **AND** the exception is not an `ApprovalRefusedException` and the status is unchanged

### Requirement: An approver with a user id is required
`Approve` SHALL reject a null approver and a blank user id before it loads or changes the invoice. Both cases throw `ArgumentException` (parameter `approver`) with message `An approver with a user id is required.` `Handle` maps `ArgumentException` to 400. `ArgumentNullException` was considered for null and not used: it is already an `ArgumentException`, so the HTTP status would not change, and one guard covers both cases.

#### Scenario: null approver
- **GIVEN** a pending invoice
- **WHEN** `Approve` is called with a null approver
- **THEN** the attempt throws `ArgumentException` with message `An approver with a user id is required.`
- **AND** `Status`, `DecidedBy`, `DecidedAt`, and `Approvals` are unchanged

#### Scenario: blank user id
- **GIVEN** a pending invoice
- **WHEN** `Approve` is called with an approver whose user id is blank
- **THEN** the attempt throws `ArgumentException` with message `An approver with a user id is required.`
- **AND** the invoice is unchanged

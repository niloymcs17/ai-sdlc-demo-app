---
name: write-approval-tests
description: >-
  Writes xUnit tests for invoice approval rules, including exact-limit and
  refusal cases. Use when writing or extending unit tests for approval limits,
  ApprovalService, or a refused approval.
---

For every rule write tests for: exact limit, limit+1 (rejected), well below limit.
For every rejection assert (a) the specific reason text, (b) invoice status unchanged,
(c) approver list unchanged. Use Theory/InlineData for boundaries.
Test names: `Approve_<Condition>_<ExpectedResult>`, for example `Approve_ManagerAtExactLimit_Approves`.
Do not assert on private state or mirror the implementation.

Test project: `backend/tests/InvoiceApp.Tests`. One test class per class under test. Approval rules go in `ApprovalServiceTests`.

Build the service with `new ApprovalService(new FakeInvoiceRepository(invoice))`.

`FakeInvoiceRepository` returns the same reference you seeded. To prove nothing changed, snapshot first:

```csharp
var statusBefore = invoice.Status;
var approvalsBefore = invoice.Approvals.ToList();   // Phase 2
var refusal = Assert.Throws<ApprovalRefusedException>(() => service.Approve("INV-1", manager));
Assert.Equal(expectedReason, refusal.Reason);
Assert.Equal(statusBefore, invoice.Status);
Assert.Null(invoice.DecidedBy);
Assert.Null(invoice.DecidedAt);
Assert.Equal(approvalsBefore, invoice.Approvals);
```

`decimal` cannot be an attribute argument. Do not write `[InlineData(50000m)]`. Pass `string` or `double` and convert with `decimal.Parse(value, CultureInfo.InvariantCulture)`, or use `[MemberData]`.

For each limit cover exactly the limit (allowed), limit + 0.01 and limit + 1 (refused), and well below (allowed).

For R4, two distinct `Approver` objects with the same `UserId` count as the same user.

Assert the reason code and the message text. Assert both refusal checks independently and combined.

Never touch production code. Never weaken an existing assertion.

# Tasks

Phase 1 is the single-approver path (R1, R2, R5). Phase 2 is the two-approver path (R3, R4, R5). No task mixes the two phases.

Compare amounts only with `ApprovalLimits.ManagerLimit` and `ApprovalLimits.DirectorLimit`. Do not copy limit literals into controllers, `Program.cs`, `ApprovalService`, tests' production paths, or `app.js`.

| ID | Task | Files | Test to write first | Rule(s) / edge cases covered |
|---|---|---|---|---|
| T1.1 | implementer — Domain types: `ApprovalLimits`, `Roles`, `Approver`, `ApprovalRefusedException` plus reason codes and messages | `backend/src/InvoiceApp.Domain/ApprovalLimits.cs`, `backend/src/InvoiceApp.Domain/Roles.cs`, `backend/src/InvoiceApp.Domain/Approver.cs`, `backend/src/InvoiceApp.Domain/ApprovalRefusedException.cs` | none (types only; compile) | R1, R2, R5 groundwork |
| T1.2 | test-writer — Phase 1 tests: E1, E2, E3, E10, E12, E13, D13 placeholder, no-mutation on every refusal; update existing call sites (D12) | `backend/tests/InvoiceApp.Tests/ApprovalServiceTests.cs` | these tests are red | R1, R2, R5 |
| T1.3 | implementer — `ApprovalService.Approve(string, Approver)`: validate amount, role, and limit before mutating | `backend/src/InvoiceApp.Domain/ApprovalService.cs` | T1.2 turns green | R1, R2, R5 |
| T1.4 | implementer — API wiring: controller builds `Approver` from the session; extend `RequireRole`; demo users; seed over-limit invoices | `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`, `backend/src/InvoiceApp.Api/appsettings.json`, `backend/src/InvoiceApp.Api/InMemoryInvoiceRepository.cs` | manual smoke | R1, R2 |
| T1.5 | human — `dotnet test` green, README demo accounts, commit Phase 1 | `README.md` | `dotnet test backend/InvoiceApp.sln` green | Phase 1 close |
| T2.1 | implementer — `ApprovalRecord` plus `Invoice.Approvals`; single approvals also append a record | `backend/src/InvoiceApp.Domain/ApprovalRecord.cs`, `backend/src/InvoiceApp.Domain/Invoice.cs`, `backend/src/InvoiceApp.Domain/ApprovalService.cs` | T2.2 | R5 audit |
| T2.2 | test-writer — Phase 2 tests: E4, E5, E6, E7, E9, E11, E14, E16, no-mutation including `Approvals`; supersede the D13 placeholder | `backend/tests/InvoiceApp.Tests/ApprovalServiceTests.cs` | red | R3, R4, R5 |
| T2.3 | implementer — Above the Director limit: first approval recorded and stays pending; second must be a distinct Director | `backend/src/InvoiceApp.Domain/ApprovalService.cs` | T2.2 green | R3, R4, R5 |
| T2.4 | human — `dotnet test` green, commit Phase 2 | `backend/InvoiceApp.sln` | `dotnet test backend/InvoiceApp.sln` green | Phase 2 close |

## 1. Phase 1 — single approver (R1, R2, R5)

- [ ] 1.1 T1.1 implementer — Domain types: `ApprovalLimits`, `Roles`, `Approver`, `ApprovalRefusedException` plus reason codes and the D6 messages. `ApprovalRefusedException` derives from `InvalidOperationException`. Files: `backend/src/InvoiceApp.Domain/ApprovalLimits.cs`, `backend/src/InvoiceApp.Domain/Roles.cs`, `backend/src/InvoiceApp.Domain/Approver.cs`, `backend/src/InvoiceApp.Domain/ApprovalRefusedException.cs`. Test to write first: none (types only; compile). Covers R1, R2, R5 groundwork.
- [ ] 1.2 T1.2 test-writer — Phase 1 tests for E1, E2, E3, E10, E12, E13, the D13 placeholder (a Director above `ApprovalLimits.DirectorLimit` is refused and the invoice is unchanged), and no-mutation on every refusal. Update existing `Approve` call sites per D12: change only arguments, keep every assertion. Files: `backend/tests/InvoiceApp.Tests/ApprovalServiceTests.cs`. Test to write first: these tests are red. Covers R1, R2, R5.
- [ ] 1.3 T1.3 implementer — `ApprovalService.Approve(string, Approver)` validates amount, role, and limit before the first assignment. Above `ApprovalLimits.DirectorLimit`, refuse with the Phase 1 message from D13 and do not record an approver. Files: `backend/src/InvoiceApp.Domain/ApprovalService.cs`. Test to write first: T1.2 turns green. Covers R1, R2, R5.
- [ ] 1.4 T1.4 implementer — API wiring only: the controller builds `Approver` from the session and calls the service; extend `RequireRole` to Manager, Director, Approver, and Admin; add the D1 demo users; seed pending invoices `INV-1006` (above the Manager limit and within the Director limit), `INV-1007` (above the Director limit), and `INV-1008` (equal to the Director limit). No approval rules and no limit numbers in the controller or `Program.cs`. Files: `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`, `backend/src/InvoiceApp.Api/appsettings.json`, `backend/src/InvoiceApp.Api/InMemoryInvoiceRepository.cs`. Test to write first: manual smoke. Covers R1, R2.
- [ ] 1.5 T1.5 human — Run `dotnet test backend/InvoiceApp.sln` until green, record the demo accounts in `README.md`, and commit Phase 1 with a WHY body. Files: `README.md`. Test to write first: the suite is green. Covers the Phase 1 close.

## 2. Phase 2 — two approvers (R3, R4, R5)

- [ ] 2.1 T2.1 implementer — Add `ApprovalRecord` and `Invoice.Approvals`. Single approvals also append a record. Do not implement the second-approver rule in this task. Files: `backend/src/InvoiceApp.Domain/ApprovalRecord.cs`, `backend/src/InvoiceApp.Domain/Invoice.cs`, `backend/src/InvoiceApp.Domain/ApprovalService.cs`. Test to write first: T2.2. Covers R5 audit.
- [ ] 2.2 T2.2 test-writer — Phase 2 tests for E4, E5, E6, E7, E9, E11, E14, and E16, including no-mutation of `Approvals`. Supersede the D13 placeholder (remove only that placeholder, and replace it with E4). Files: `backend/tests/InvoiceApp.Tests/ApprovalServiceTests.cs`. Test to write first: these tests are red. Covers R3, R4, R5.
- [ ] 2.3 T2.3 implementer — Above the Director limit, record the first approval and stay `PendingApproval`. The second approver must be a distinct Director. Same-user check first (D10). Validate before mutate. Files: `backend/src/InvoiceApp.Domain/ApprovalService.cs`. Test to write first: T2.2 turns green. Covers R3, R4, R5.
- [ ] 2.4 T2.4 human — Run `dotnet test backend/InvoiceApp.sln` until green and commit Phase 2. The commit body says the D13 placeholder was superseded by the pending-second-approval behaviour. Files: `backend/InvoiceApp.sln`. Test to write first: the suite is green. Covers the Phase 2 close.

---
name: spec-to-tasks
description: >-
  Breaks an approved invoice-approval spec into small, ordered, independently
  committable tasks. Use when converting a spec into implementation tasks,
  writing a task list, or splitting Phase 1 and Phase 2 work.
---

Produce tasks that are each independently committable.

Output columns: `ID | Task | Files | Test to write first | Rule(s) / edge cases covered`.

IDs: `T1.x` for Phase 1 (R1, R2, R5, single approver) and `T2.x` for Phase 2 (R3, R4, R5, two-approver path). Split into those two phases. Never combine phases in one task.

Every task names real paths under `backend/src/InvoiceApp.Domain`, `backend/src/InvoiceApp.Api`, or `backend/tests/InvoiceApp.Tests`.

Separate production tasks (implementer) from test tasks (test-writer). A task touching `Controllers/` may only map input and call the service.

Each phase ends with a task: run `dotnet test backend/InvoiceApp.sln` green; commit with a WHY body.

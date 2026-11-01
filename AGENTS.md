# Project conventions for AI agents

Invoice approval demo app: ASP.NET Core 8 Web API with a small static web UI.
This file is the contract between humans and agents for this repo.

## Layout

- `backend/src/InvoiceApp.Domain`: business rules, no web or storage code. `ApprovalService` owns the invoice lifecycle (Draft, PendingApproval, Approved, Rejected).
- `backend/src/InvoiceApp.Api`: controllers, sign-in (`Auth/`), in-memory repository, and the web UI under `wwwroot/`.
- `backend/tests/InvoiceApp.Tests`: xUnit tests, one test class per class under test, named `<ClassUnderTest>Tests`.
- `e2e`: Playwright end-to-end tests (`tests/`) and page objects (`pages/`).
- `docs/use-cases`: short briefs for features we may build.

There is no `Services/` folder. `ApprovalService` lives in the Domain project.

## Conventions

- Controllers live in `Controllers/`, one per resource, route `api/[controller]`. Request bodies are `record` types declared inside the controller.
- Controllers inherit `ApiControllerBase` and use `Handle(...)` so service errors map to 404, 409 and 400.
- Every endpoint is behind sign-in. Restrict actions with `[RequireRole("...")]`: destructive actions are Admin only, approvals are Approver or Admin.
- Business rules go in the Domain project, never in controllers.
- Register services in `Program.cs`. Repositories are interfaces in Domain with an in-memory implementation in Api.
- Every new rule or endpoint needs tests. Use `FakeInvoiceRepository` in tests.
- Use clear names; no one-letter variables or `flag` parameters.
- End-to-end tests use page objects and role-based locators (`getByRole`, `getByLabel`), never CSS or XPath selectors.

## Approval rules

| Rule | Detail |
|---|---|
| R1 | A Manager may approve invoices with amount <= 50,000 |
| R2 | A Director may approve invoices with amount <= 500,000 |
| R3 | Amount > 500,000 needs TWO distinct approvers; the second must be a Director |
| R4 | The same user can never count as both approvers on one invoice |
| R5 | A refused attempt returns a clear reason and changes NOTHING on the invoice |

Boundaries are inclusive: 50,000 and 500,000 are allowed for the stated role. Money is `decimal`, never `double` or `float`. Users are compared by `UserId` (the username) with `StringComparison.Ordinal`, never by display name or object reference. A refused approval is not an invoice rejection: status stays `PendingApproval`. Only the explicit Reject action sets `Rejected`.

## Decisions

- D2: Only Manager and Director have approval authority. Any other role is refused with a clear reason. Admin still deletes. `RequireRole` on approvals includes Manager and Director so the domain, not a bare 403, explains the refusal.
- D4: A refused approval leaves the invoice unchanged (`PendingApproval`). It is not a rejection.
- D5: Refusal throws `ApprovalRefusedException : InvalidOperationException` with a reason code and a clear message. `Handle` already maps that to 409.
- D7: A Manager may be the first approver on an amount above 500,000. Only the second approver must be a Director.
- D9: After the first of two approvals the status stays `PendingApproval`. Record approvers on the invoice. Final approval sets `Approved`, `DecidedBy`, and `DecidedAt`.
- D10: When the second approval fails both checks, report the same-user reason first, then the Director check.

## Layering

- Controllers (`backend/src/InvoiceApp.Api/Controllers`): HTTP only. Map the session to an `Approver` and call the service. No approval rules and no limit numbers.
- Domain (`backend/src/InvoiceApp.Domain`): all approval rules, in `ApprovalService` and `ApprovalLimits`. No web and no storage code.
- Repositories: persistence only. `IInvoiceRepository` is in Domain. `InMemoryInvoiceRepository` is in Api. `FakeInvoiceRepository` is in the test project.
- UI (`backend/src/InvoiceApp.Api/wwwroot/app.js`): display only. No limit numbers.
- Dependency direction: controller to domain service to repository. Never backwards.

## Single source of limits

`ApprovalLimits.ManagerLimit` and `ApprovalLimits.DirectorLimit` in `backend/src/InvoiceApp.Domain/ApprovalLimits.cs` are the only copies of the limits. No literals `50000` or `500000` anywhere else, including controllers, tests' production paths, and `app.js`.

## Validate before mutate

`ApprovalService` must finish every check before the first assignment to the invoice. Repositories return live references, so a mutation before a thrown refusal is already visible to the caller. Snapshot status and approver fields before the call in tests, then compare after.

## Commands

- Build: `dotnet build backend/InvoiceApp.sln`
- Test: `dotnet test backend/InvoiceApp.sln`
- Mutation: `cd backend/tests/InvoiceApp.Tests; dotnet stryker`
- E2E: `cd e2e; npm test`
- Run: `dotnet run --project backend/src/InvoiceApp.Api` then open http://localhost:5000

## Demo accounts (not real credentials)

- `demo.approver` / `ChangeMe123!` (role Approver)
- `demo.admin` / `ChangeMe123!` (role Admin)
- `demo.manager` (Manager), `demo.director` and `demo.director2` (Director), same demo password, once Phase 1 adds them

## Working agreements

- One task per prompt.
- Write or update tests in the same change as the code.
- Never edit existing assertions to make a test pass.
- On a refused approval, mutate nothing.
- No approval rules in controllers or in `Program.cs`.
- Commit messages: `type: what changed`, plus a body explaining WHY.

## Test conventions

- Name tests `Method_Condition_ExpectedResult`.
- Cover boundaries with `[Theory]` and `[InlineData]`.
- Snapshot state before the call and compare after. Do not compare an object to itself.

## Review expectations

Reviewers check, in this order:

1. Boundary values: 50,000 / 50,001 / 500,000 / 500,001.
2. A refusal leaves invoice state unchanged (status and approver list).
3. The same user cannot be both approvers; the second approver must be a Director.
4. Layering is respected; limits and rules are not duplicated.
5. Tests assert behaviour and reasons, not implementation details.
6. No limit numbers in `app.js` or controllers.

Output findings with the review table: `| # | Finding | Severity | Location | Suggested fix | Verdict | Reason |`.

## Agents, skills and commands

Placeholders until tasks 08 and 09 add them:

- Agents: spec-agent, implementer, test-writer, reviewer (`.cursor/agents/`).
- Skills: write-approval-tests, review-diff, spec-to-tasks (`.cursor/skills/`).
- Commands: /review-pr (`.cursor/commands/`).

## Never

- Commit real credentials, tokens or customer data.
- Add business logic to `Program.cs` or controllers.

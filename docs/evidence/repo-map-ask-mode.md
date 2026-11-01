# Repo map (read-only pass)

Asked in a fresh read-only explore agent, not a separate Cursor Ask-mode chat. This session cannot open Ask mode or turn off User Rules. The prompt was the task's five questions. The answer below is that agent's, lightly trimmed of the repeated absolute-path prefix `c:\My Files\ai-sdlc-demo-app\`.

## Answer

### 1) Controllers, services, domain models, repositories

| Layer | Path |
|---|---|
| Solution | `backend/InvoiceApp.sln` |
| Domain | `backend/src/InvoiceApp.Domain/` |
| API (web + persistence) | `backend/src/InvoiceApp.Api/` |

Controllers (`backend/src/InvoiceApp.Api/Controllers/`):

- `ApiControllerBase.cs` — shared `Handle(Func<Invoice>)` error mapping
- `AuthController.cs`
- `InvoicesController.cs` — CRUD, list, submit
- `ApprovalsController.cs` — approve/reject

Auth (API, not domain) in `backend/src/InvoiceApp.Api/Auth/`: `SessionAuthMiddleware.cs`, `SessionStore.cs`, `UserSession.cs`, `RequireRoleAttribute.cs`.

The agent grouped these under "Services (domain)":

- `backend/src/InvoiceApp.Domain/ApprovalService.cs` — invoice lifecycle
- `backend/src/InvoiceApp.Domain/DiscountCalculator.cs` — not wired into approval

Registered in `backend/src/InvoiceApp.Api/Program.cs`: `IInvoiceRepository` → `InMemoryInvoiceRepository`, scoped `ApprovalService`.

Domain models: `Invoice.cs`, `InvoiceStatus.cs`.

Repository: `IInvoiceRepository` in Domain, `InMemoryInvoiceRepository` in Api, `FakeInvoiceRepository` in `backend/tests/InvoiceApp.Tests/`.

### 2) Tests and framework

- `backend/tests/InvoiceApp.Tests/InvoiceApp.Tests.csproj` — xUnit, `Microsoft.NET.Test.Sdk`, coverlet, `net8.0`
- Only test class found: `ApprovalServiceTests.cs`
- E2E scaffold: `e2e/package.json` (`@playwright/test`), `e2e/playwright.config.ts`
- The agent said `e2e/tests/` and `e2e/pages/` do not exist

### 3) How invoice approval works today

`ApprovalService`: Draft → PendingApproval → Approved or Rejected.

| Method | Return type | Behavior |
|---|---|---|
| `Submit(string invoiceId)` | `Invoice` | Draft only → `PendingApproval` |
| `Approve(string invoiceId, string approver)` | `Invoice` | Pending → `Approved`, sets `DecidedBy`, `DecidedAt` |
| `Reject(string invoiceId, string approver, string reason)` | `Invoice` | Pending, non-empty reason → `Rejected`, sets `DecidedBy`, `DecidedAt`, `DecisionNote` |

`Find` throws `KeyNotFoundException`. `EnsurePending` throws `InvalidOperationException`.

`Invoice` stores `InvoiceStatus Status` (default `Draft`), `string? DecidedBy`, `DateTime? DecidedAt`, `string? DecisionNote`. Enum: `Draft`, `PendingApproval`, `Approved`, `Rejected`.

HTTP: `POST api/invoices/{id}/submit`, `POST api/approvals/{invoiceId}/approve`, `POST api/approvals/{invoiceId}/reject`. Approve and reject require role Approver or Admin and pass `HttpContext` session username.

### 4) UI

Static files: `backend/src/InvoiceApp.Api/wwwroot/index.html`, `app.js`, `styles.css`. HTML, CSS, and vanilla JavaScript. `fetch` to `/api/...` with `Authorization: Bearer`.

`refresh()` loads `GET /api/invoices`. The Approvals list keeps `status === 'PendingApproval'`. Approve calls `decide('/approvals/${id}/approve', {})`. Reject calls `decide('/approvals/${id}/reject', { reason })`. Draft rows submit with `POST /api/invoices/${id}/submit`. Errors go to `#decision-error`.

### 5) AGENTS.md, .cursor, review prompt, docs

| Item | Present? | Path |
|---|---|---|
| AGENTS.md | Yes | `AGENTS.md` |
| CLAUDE.md | Yes | `CLAUDE.md` (`@AGENTS.md`) |
| .cursor | Yes | `.cursor/hooks.json`, `.cursor/hooks/log_event.py`, `check_commit_msg.py`, `validate_review.py` |
| Review prompt / commands | No dedicated file | No `.cursor/commands/`, `.cursor/skills/`, or `.cursor/agents/` |
| docs | Yes | `docs/use-cases/UC-1-bulk-approval.md`, `UC-2-partial-approval.md`, `docs/evidence/` |

## Comparison with task 00

Checked the cited paths on disk.

- The agent did not invent a `Services/` folder. It did put `ApprovalService.cs` under a "Services (domain)" heading. The file is in `backend/src/InvoiceApp.Domain/`, which matches task 00.
- Wrong: it said `e2e/tests/` and `e2e/pages/` do not exist. Both directories are on disk. They are empty, which is why a file search missed them. Task 00 lists them as the scaffolded layout.
- The approval methods, return type `Invoice`, status enum, and `DecidedBy` match `ApprovalService.cs` and `Invoice.cs`.
- The UI technology and the `decide()` approve/reject calls match `wwwroot/app.js`.
- AGENTS.md, `.cursor/hooks/`, and `docs/` are present. There is still no workshop review prompt in the repo, which matches task 00.

## Summary

Approval limits by role (R1 to R5). Built in two feature commits plus tests, UI and end-to-end coverage.

## Commits

- `3f91865` chore: add Cursor hooks for logging and structured output, plus annotation tooling
- `390b244` chore: stop tracking Python bytecode
- `ad88852` docs: extend AGENTS.md with approval rules, layering, commands and review expectations
- `d92caf6` chore: add skills for spec-to-tasks, approval tests and diff review
- `f4a7db5` chore: add spec, implementer, test-writer and reviewer agents and review command
- `6be33fe` docs: save repo-map evidence and the OpenSpec scaffold
- `cfeca2b` docs(spec): add approval-limits specification and task breakdown via OpenSpec
- `4a2d0b5` feat: enforce approval limits by role
- `cc49686` test: save Phase 1 red and green runs and set demo seed amounts
- `ea8b7e9` feat: require two distinct approvers above 500k, second must be a director
- `a105ea2` test: add Stryker config scoped to approval rules, record BEFORE mutation score
- `5e9fa60` test: strengthen approval tests after review and mutation analysis
- `454165f` docs: add AFTER mutation score and survivor analysis
- `a360f51` test(e2e): add Playwright test for rejected over-limit approval
- `2df75f7` fix: show approvers on the invoice table (review finding 1)

## Test evidence

dotnet test: 38 passed, 0 failed

Stryker: before 84.75% -> after 100.00%

Playwright: 1 passed

Each of the 14 commits before the review fix passed `dotnet test` on its own. The review fix does not change domain logic, so Stryker was not rerun.

## AI review findings and verdicts

| # | Finding | Severity | Location | Verdict | Reason / commit |
|---|---|---|---|---|---|
| 1 | The invoices table has no Approvals cell, so the end-to-end test cannot see `None` after a refused approval. | high | `e2e/tests/over-limit-approval.spec.ts:21` | **Fixed** | commit `2df75f7` |
| 2 | Overlapping `Approve` calls can both see an empty approver list and both append, so a double-click or two Managers can approve an amount above the Director limit. | high | `ApprovalService.cs:42` | **Deferred** | In-memory demo store. Needs optimistic concurrency on `Update`. Write-up: `docs/review/deferred-concurrency.md`. Not filed on the fork: `gh` is not logged in. |

Fixed 1, dismissed 0, deferred 1.

## Links

AGENTS.md, openspec/changes/add-approval-limits-by-role, .ai-log/ai-interactions.md, ASSIGNMENT.md

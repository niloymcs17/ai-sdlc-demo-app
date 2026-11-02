# Assignment write-up

Assessor entry point for the approval-limits work on `feature/approval-limits-by-role`. Every claim below points at a file or a commit. There is no pull request yet: GitHub CLI was not authenticated, so the branch was not pushed (`docs/review/review-verdicts.md`).

Fork: [niloymcs17/ai-sdlc-demo-app](https://github.com/niloymcs17/ai-sdlc-demo-app). Upstream: [NITIN1670/ai-sdlc-demo-app](https://github.com/NITIN1670/ai-sdlc-demo-app).

## 1. Overview

Managers may approve an invoice at or under 50,000. Directors may approve at or under 500,000. Above 500,000 the invoice stays pending until a second, different approver signs, and that second approver must be a Director. A refused attempt leaves the invoice pending. The rules live in `ApprovalService` and `ApprovalLimits` (`backend/src/InvoiceApp.Domain/`).

| Tool | Version | Source |
|---|---|---|
| Cursor | 3.27.22 | `docs/evidence/hook-payload-keys.md` |
| Model on the logged prompts | `grok-4.7-medium-fast` and `grok-4.7` | `.ai-log/ai-interactions.md` |
| OpenSpec | change `add-approval-limits-by-role` | `openspec/changes/add-approval-limits-by-role/` |
| Stryker.NET | 4.13.0 | `docs/evidence/stryker-before.txt` (line `Version: 4.13.0`) |
| Playwright | `@playwright/test` 1.63.0 | `e2e/package-lock.json` |
| .NET | 8.0 | `.github/workflows/ci.yml` |

`docs/evidence/tool-versions.txt` is not in the repo. The versions above are the ones the evidence files actually record.

## 2. Context setup

`AGENTS.md` (`ad88852`) is the contract a cold agent gets for this repo. It adds the R1–R5 table, the decision lines, layering (controllers and `wwwroot` have no rules and no limit numbers), `ApprovalLimits` as the only copy of the ceilings, validate-before-mutate, the build/test/mutation/E2E commands, test names, and the review checklist. `CLAUDE.md` only points at `AGENTS.md`.

The Ask-mode map was run as a read-only explore agent, not a separate Ask chat (`docs/evidence/repo-map-ask-mode.md`). It did not invent a `Services/` folder. It was wrong that `e2e/tests/` and `e2e/pages/` were missing; both directories existed and were empty.

The control run, with `AGENTS.md`, `CLAUDE.md`, and `.cursor/rules` moved aside, is `docs/evidence/before-agents-md.diff`. Tests then: 16 passed, 0 failed (`docs/evidence/before-agents-md-test-run.txt`). The treatment diff was not saved: `docs/evidence/after-agents-md.diff` and `docs/evidence/agents-md-comparison.md` are not in the repo, and there is no `docs: add before/after AGENTS.md context experiment evidence` commit. The treatment column is therefore blank on purpose.

| Aspect | Minimal context (`before-agents-md.diff`) | Extended AGENTS.md |
|---|---|---|
| Where logic landed | `ApprovalService.EnsureWithinRoleLimit`, called before the status write. The controller only passed `session.Role` through. | Not recorded |
| Boundary operator | `invoice.Amount <= limit` | Not recorded |
| Limits | Named constants on `ApprovalService` (`ManagerApprovalLimit`, `DirectorApprovalLimit`), not `ApprovalLimits.cs`. Tests still used `500_000` literals. | Not recorded |
| Money type | `decimal` | Not recorded |
| Refusal reason | `InvalidOperationException` with the role name and a formatted limit. No `ApprovalRefusedException`. | Not recorded |
| Mutated before validation? | No. The new check returns before `Status` is set. | Not recorded |
| Refusal set `Rejected`? | No. It throws. | Not recorded |
| Roles | New `ApprovalRoles` strings, compared with `OrdinalIgnoreCase` | Not recorded |
| Tests | Added. The saved run is 16 passed. | Not recorded |
| Build + tests green? | Yes, 16 passed, 0 failed | Not recorded |
| Files touched | `README.md`, `ApprovalsController.cs`, `InMemoryInvoiceRepository.cs`, `appsettings.json`, `ApprovalService.cs`, `ApprovalServiceTests.cs`, plus new role types in that diff | Not recorded |

## 3. Skills, hooks, agents, commands

| Name | Purpose | Path | Where used |
|---|---|---|---|
| spec-to-tasks | Split an approved spec into ordered tasks | `.cursor/skills/spec-to-tasks/SKILL.md` | Spec work. The log lists the skill as used. |
| write-approval-tests | xUnit tests for limits and refusals, including unchanged state | `.cursor/skills/write-approval-tests/SKILL.md` | Unit tests for `ApprovalService` |
| review-diff | One findings table, verdicts left pending | `.cursor/skills/review-diff/SKILL.md` | Task 18 review |
| spec-agent | Edits `openspec/**` only | `.cursor/agents/spec-agent.md` | OpenSpec change `cfeca2b` |
| implementer | Edits `backend/src/**` only | `.cursor/agents/implementer.md` | Feature commits, and the approvers-column fix `2df75f7` |
| test-writer | Edits `backend/tests/**` and `e2e/**` only | `.cursor/agents/test-writer.md` | Approval tests and the Playwright spec `a360f51` |
| reviewer | Read-only diff review | `.cursor/agents/reviewer.md` | Task 18. Raw table: `docs/evidence/ai-review-raw.md` |
| /implement-task | Delegate one task to the implementer | `.cursor/commands/implement-task.md` | Implementation prompts |
| /write-tests | Delegate tests to the test-writer | `.cursor/commands/write-tests.md` | Test prompts |
| /review-pr | Delegate the review | `.cursor/commands/review-pr.md` | Task 18. The workshop prompt file was never saved; the command says so. |

OpenSpec also installed its own skills and `opsx-*` commands under `.cursor/skills/` and `.cursor/commands/`. The interaction log records `openspec-propose` as a skill that was read.

Hooks (`.cursor/hooks.json`):

| Hook | What it forces |
|---|---|
| `check_commit_msg.py` on `beforeShellExecution` | A commit with no WHY body is denied before git runs. Evidence: `docs/evidence/hook-commit-blocked.txt` (`git commit -m "wip"` → `Commit blocked: message needs a body explaining WHY.`). |
| `validate_review.py` on `stop` | A message that starts with `REVIEW-FINDINGS:` and has no findings table gets one reformat follow-up. Evidence: `docs/evidence/hook-review-reformat.md`. |
| `log_event.py` on prompt, read, edit, shell, MCP, sub-agent start/stop, and stop | Appends `.ai-log/ai-interactions.jsonl`. A `dotnet test` shell is also summarised. Evidence: `docs/evidence/hook-test-summary.txt` (`TESTS: passed=7 failed=0 skipped=0 total=7`). |
| Payload keys actually seen | `docs/evidence/hook-payload-keys.md` |

## 4. Spec-driven development

OpenSpec was chosen (D15) so the rules, the edge cases, and the task split exist before implementation. The change is `openspec/changes/add-approval-limits-by-role/`: `proposal.md`, `design.md`, `specs/approval-limits/spec.md`, `tasks.md`. Commit `cfeca2b`.

Decisions D1–D16 are the table in `design.md`. The two that needed a human call:

- E9 is D7. A Manager may be the first approver on an amount above the Director limit. Status stays `PendingApproval` and the approval is recorded. The second approver is the one who must be a Director.
- E10 is D8. Zero or a negative amount is refused with `Invoice amount must be greater than zero.`, even though create already rejects that amount.

D14 keeps approval records when someone later rejects the invoice. That is an audit trail, not a half-applied approval.

## 5. Implementation steps

`git log --oneline main..HEAD`:

| Commit | What | Why |
|---|---|---|
| `3f91865` | Hooks, `annotate.py`, `render_log.py` | The later evidence has to be captured as it happens. |
| `390b244` | Stop tracking Python bytecode | Generated files were showing up as source. |
| `ad88852` | Extended `AGENTS.md` and the layering rule | A cold agent needs the rules, the paths, and the commands. |
| `d92caf6` | The three project skills | Specs, tests, and reviews should follow one written procedure. |
| `f4a7db5` | spec-agent, implementer, test-writer, reviewer, and `/review-pr` | Each role is limited to its own directories. |
| `6be33fe` | Repo-map evidence and the OpenSpec scaffold | The Ask pass and the tool install are part of the trail. |
| `cfeca2b` | Approval-limits spec and tasks | Implementation follows a written decision table. |
| `4a2d0b5` | Single-approver limits (R1, R2, R5) | Phase 1 has to pass on its own, before the two-approver rule. |
| `cc49686` | Phase 1 red and green runs, demo seed amounts | The red run shows the tests failed before the behaviour existed. |
| `ea8b7e9` | Two distinct approvers above the Director limit; the second must be a Director | Dual control. The same user cannot count twice. |
| `a105ea2` | Stryker config and the BEFORE score | The score is scoped to the approval types. |
| `5e9fa60` | Stronger tests after review and mutation | Survivors were tests that did not observe the behaviour. |
| `454165f` | AFTER score and the survivor write-up | The comparison is text, not a screenshot. |
| `a360f51` | Playwright test for a refused over-limit approval | The domain test does not show the refusal in the UI. |
| `2df75f7` | Approvals column on the invoice table | Review finding 1. The E2E assertion had no cell to read. |
| `d02c51f` | Review verdicts and the PR description | Every finding gets one verdict. |

Each of these commits passed `dotnet test` in its own worktree (`docs/review/review-verdicts.md`). History was not rewritten with `git rebase -x`, because the branch was already on the fork and a replay would have needed a force push.

Traps from the two-approver step, and what the tests showed the generated code and tests actually did (`docs/evidence/test-review-notes.md`):

| Trap | What happened |
|---|---|
| Same user compared by name or by object reference | The tests do compare `UserId` on two distinct objects (`Approve_SameUserIdOnDistinctApproverObjects_RefusesAsSameUser`). This trap was covered, not left open. |
| Second refusal leaves a half-changed approver list | The shared `AssertUnchanged` helper snapshotted status and decided-by fields and did not copy `Approvals`. A refusal that appended a record would still pass. The helper was fixed. |
| Second-approver check applied to the first approver | `Approve_ManagerFirstThenDirectorAboveDirectorLimit_ApprovesOnSecond` keeps the invoice pending after the Manager and approves on the Director. |
| Director alone on an amount above the Director limit marks it fully approved | The theory originally had Director limit + 0.01 only. Director limit + 1 was added. Status stays pending with one record. |
| Manager accepted as the second approver | Covered by `Approve_SecondApproverIsManager_RefusesAndLeavesApprovalsUnchanged` and the two-Manager test. Kept. |
| Same-user check skipped when the role is also wrong | `Approve_SameManagerApprovesTwice_ReportsSameUserBeforeDirectorCheck` asserts the same-user code and not the Director code. Kept. |

## 6. Test review

Source: `docs/evidence/test-review-notes.md`. Of the 24 tests in that table: **Keep 11, Modify 12, Delete 1.**

Three things the generated tests got wrong:

1. Several tests asserted only the exception type. `Submit_RejectsInvoiceThatIsNotADraft`, `Approve_ThrowsWhenInvoiceDoesNotExist`, `Reject_RequiresAReason`, and `Approve_BlankUserId_ThrowsArgumentException` never read `Message`. Stryker emptied those strings and the tests stayed green.
2. `AssertUnchanged` did not copy `Approvals`. A refusal that appended an `ApprovalRecord` before throwing still passed the Manager-over-limit test and the amount, role, and blank-user tests.
3. Success tests read the live object the service had already mutated, and `FakeInvoiceRepository.Update` threw the saved invoice away. Removing `_repository.Update` changed nothing those tests could see.

The one deletion is `Approve_RejectsInvoiceThatIsStillADraft`. It repeats the Draft row of `Approve_InvoiceNotPending_ThrowsStatusErrorRatherThanRefusal` and checks less. No existing assertion was weakened to make a test pass. After the edits, `dotnet test` is 38 passed (`docs/evidence/after-review-test-run.txt` is the run saved with that work; the later Playwright fix did not change the count).

## 7. Mutation testing

Same config both times: `backend/tests/InvoiceApp.Tests/stryker-config.json`. Stryker.NET 4.13.0. Mutants are only `ApprovalService`, `ApprovalLimits`, `Approver`, `ApprovalRecord`, and `ApprovalRefusedException`. `DiscountCalculator.cs` is outside that list: it has no tests, and a `**/Domain/*.cs` pattern would have scored its mutants as unrelated survivors. The report shows those as 22 ignored mutants, not in the score. Ignored mutants and compile-error mutants are not in the score.

Both runs created 105 mutants, skipped 46 (5 compile errors, 17 block-already-covered, 24 mutate-filter), and tested 59. No mutant was `NoCoverage` or `Timeout`.

```
BEFORE: Mutation score: 84.75%  (killed 50, survived 9, no coverage 0, timeout 0)
AFTER:  Mutation score: 100.00%  (killed 59, survived 0, no coverage 0, timeout 0)
```

BEFORE is tag `mutation-before` (`ea8b7e9`). AFTER is the strengthened tests in `5e9fa60` (38 tests). Full write-up: `docs/evidence/mutation-comparison.md`. Logs: `docs/evidence/stryker-before.txt`, `docs/evidence/stryker-after.txt`.

All nine survivors in `docs/evidence/mutation-survivors-before.md` were killed. None was accepted as equivalent. The killing tests are S1 `Submit_RejectsInvoiceThatIsNotADraft`, S2 `Submit_MovesDraftToPendingApproval`, S3 the blank and null approver tests, S4 `Approve_SetsStatusApproverAndTimestamp`, S5 `Reject_RequiresAReason`, S6 `Reject_InvoiceNotPending_ThrowsAndChangesNothing`, S7 `Reject_RecordsReasonAndApprover`, S8 `Approve_ManagerOnAlreadyApprovedInvoiceAtManagerLimit_Approves`, S9 `Approve_ThrowsWhenInvoiceDoesNotExist`.

## 8. Playwright

`e2e/tests/over-limit-approval.spec.ts` (`a360f51`, page objects under `e2e/pages/`). One test. It creates a fresh invoice of 75000 through the API with a unique customer name, submits it, signs in as `demo.manager`, and clicks Approve. The alert must match `/exceeds the Manager approval limit/i`. The page is reloaded, because a refusal does not refetch the lists, and the invoice row must still say Pending Approval and None. The invoice must still be on the Approvals list. Locators are `getByRole` and `getByLabel` only. There is no `waitForTimeout`.

Run: `cd e2e; npm test`.

`docs/evidence/playwright-run.txt`: `1 passed`. Headed was run once and also passed; that output was not saved as a separate file.

Negative check: `ManagerLimit` was raised to `100_000m` and the same test was rerun. It failed because the alert was not found (`docs/evidence/playwright-negative-check.txt`, `1 failed`). The limit was reverted to `50_000m`. The raised value is not in the tree.

The first UI build had no Approvals cell, so that assertion could not see `None`. The column was added in `2df75f7`, and `npm test` was `1 passed` against that build.

## 9. AI review and verdicts

Raw table, verdicts still `PENDING`: `docs/evidence/ai-review-raw.md`. Judgements: `docs/review/review-verdicts.md`. Intended PR body: `docs/review/pr-description.md`.

**Fixed 1, dismissed 0, deferred 1.** There is no pull request URL.

| # | Verdict | Where |
|---|---|---|
| 1. The invoices table never rendered an Approvals cell, so the E2E test could not see `None`. | **Fixed** in `2df75f7` | The claim matched `index.html` and `renderInvoices` at review time. |
| 2. Two overlapping `Approve` calls can both see an empty approver list and both append. | **Deferred** | The claim is true. `GetById` returns the live object and drops the lock before the checks. Write-up: `docs/review/deferred-concurrency.md`. No GitHub issue number: `gh` was not logged in. |

The reviewer was not wrong about either claim. The suggested fix for finding 2 was not taken. A lock inside `ApprovalService`, plus disabling the button, would not stop two API clients, and a domain lock would pull storage into the domain project. The recorded decision is to defer it until `Update` can reject a stale version.

The `stop` hook did not ask for a reformat. The table already had the review-diff columns.

## 10. AI interaction log

| Item | Path |
|---|---|
| Captured events | `.ai-log/ai-interactions.jsonl` |
| Human annotations | `.ai-log/human-annotations.jsonl` |
| Rendered tables | `.ai-log/ai-interactions.md` |
| Renderer | `scripts/render_log.py` |
| Annotator | `scripts/annotate.py` |

Field ownership, copied from task 05:

| Field | Source |
|---|---|
| id, timestamp, session_id, ai_tool, model, request, tools, MCP, sub-agents, files, test commands, commit | Hook-captured |
| skills_used | Inferred from reads under `.cursor/skills/` (Cursor has no skill-call event) |
| mode, stage | Set by you (`AI_MODE` / `AI_STAGE` env vars or `--mode` / `--stage`) |
| ai_output_summary, suggestion, limitation | Agent-drafted, you refine |
| human_decision, human_changes, outcome, action_verification | **You write these** with `annotate.py` |

Summary from `.ai-log/ai-interactions.md` after the last `python scripts/render_log.py` for this write-up:

| Metric | Value |
|---|---|
| Total interactions | 587 |
| Outcome | success=20, partial=3, rework=1 |
| Mode | agent=581, ask=1, edit=1, plan=4 |
| Skills used | openspec-propose, review-diff, spec-to-tasks, write-approval-tests |
| Sub-agents used | explore, general-purpose, implementer, reviewer, spec-agent, test-writer, unknown |
| MCP tools used | cursor-ide-browser: browser_cdp, browser_click, browser_fill, browser_lock, browser_navigate, browser_press_key, browser_snapshot |
| Human interventions | 24 |

Cursor limitations, from the evidence rather than from a guess:

- `mode` is not copied out of the hook payload. `docs/evidence/hook-payload-keys.md` shows `composer_mode` on `beforeSubmitPrompt`, but the log's `mode` is whatever annotation or the hook default wrote. Task 07 had to be tagged afterwards with `--mode ask` because the hook cannot see the chat mode.
- `skills_used` is inferred when a file under `.cursor/skills/` is read. There is no skill-call event (`docs/evidence/hook-payload-keys.md` does not list one).
- `subagentStart` and `subagentStop` produced no payload on the first dump (`hook-payload-keys.md`, "Not produced by this run"). They did fire later: `.ai-log/ai-interactions.jsonl` has `subagent_start` and `subagent_stop` lines, including the task 09 check. In that check the custom `reviewer` type could not be selected, so the role was pasted into an `explore` agent, and that agent was able to create `test.txt` (`docs/evidence/reviewer-readonly.md`). By task 18 the `reviewer` sub-agent did run and stayed read-only.
- No free-tier cutoff is recorded in the log or the evidence files.
- The repo-map pass and the first reviewer check were not separate Ask or reviewer chats. They were explore agents with the task prompt pasted in (`repo-map-ask-mode.md`, `reviewer-readonly.md`).

## 11. Reflection

Drafted from the evidence files so this section is not empty. Edit it before you submit. It is not a claim that the story was flawless.

The useful part was speed on a shape that was already written down: the OpenSpec decision table, the test names, and the commit split. The agents followed directory limits once those agents existed (implementer did not edit tests; the reviewer did not edit files in task 18).

The failures were about what a test can see. Generated tests checked the exception type and the live object, so mutants that emptied a message or skipped `Update` survived. That only showed up because Stryker was run and the nine survivors were read, not because the first green `dotnet test` was wrong. The E2E test was written against an Approvals cell the page did not render; the review caught it and the column was added afterwards. The context experiment is only half recorded: the control diff is saved, the treatment diff is not, so this write-up does not pretend `AGENTS.md` changed the result by a measured amount.

Judgement that was not left to the agent: D7 (a Manager may be first above the Director limit), D8 (zero and negative amounts are refused at approval time), deleting the weaker duplicate test instead of keeping both, refusing to treat the concurrency finding as a domain lock, and not force-pushing a rebase of commits that were already on the fork. The concurrency hole is still open. It is written down in `docs/review/deferred-concurrency.md` because the demo store cannot express a version check yet.

## 12. Demo video

Not recorded yet. Task 20 replaces this line with the URL.

Video: `(task 20 — paste the link here)`

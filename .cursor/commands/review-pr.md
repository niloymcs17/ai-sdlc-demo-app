Delegate to the reviewer sub-agent. It must:

1. Run `git diff main...HEAD` and `git log --oneline main..HEAD`.
2. Review the diff using the review-diff skill AND the workshop review prompt below.
3. Output only the REVIEW-FINDINGS table. Verdict and Reason stay PENDING.

## Workshop review prompt

The workshop handout is not in the course folder (`c:\My Files\Nagp AI SDLC`) and was not saved at `c:\My Files\Nagp AI SDLC\workshop-review-prompt.md` (task 02 step 6). Paste that prompt here verbatim before task 18. Do not replace this note with a prompt that was not in the workshop.

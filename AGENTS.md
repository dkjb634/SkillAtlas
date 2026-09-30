# Repository instructions

## Pull request memory

When the user explicitly authorizes publishing completed work and opening a pull request to `main` (for example, “push that and make a PR to main”), read and follow [the `pr-memory` skill](.agents/skills/pr-memory/SKILL.md). Before pushing, prepare a concise memory entry under `memory/branch_<UTC timestamp>/<task-name-summary>.md` and include it in the PR branch. Do not create a memory entry merely because a task is complete or locally committed.

Keep the entry limited to the work included in that PR. Do not stage or record unrelated user changes, and never include secrets or credentials.

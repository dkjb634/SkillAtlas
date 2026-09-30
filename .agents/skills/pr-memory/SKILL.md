---
name: pr-memory
description: Archive the important changes from a task when the user authorizes pushing the work and opening a pull request to main.
---

# Pull request memory

Use this workflow only after the user clearly authorizes publishing the completed task and opening a PR to `main` (for example, “push that and make a PR to main”). A general confirmation that work is done is not authorization to publish and does not trigger an archive.

Before pushing, create one Markdown entry at:

```text
memory/branch_<UTC timestamp>/<task-name-summary>.md
```

Use `YYYYMMDDTHHmmssZ` for the UTC timestamp and a short, descriptive kebab-case filename without `.md` in the placeholder. Record the actual source branch in the entry; `branch_<timestamp>` is the archive directory name, not a replacement for the Git branch name.

Summarize only changes included in this PR. Inspect the final diff and include the most important behavior or design decisions, plus representative code snippets copied from the changed files with their paths and language labels. Keep snippets useful and concise rather than reproducing whole files. Also record the task, source branch, target (`main`), UTC date, and the tests or checks run and their results. If a PR URL is available, include it.

Use this structure, adapting sections as needed:

~~~~markdown
# <Task summary>

- Date (UTC): <timestamp>
- Branch: <source branch>
- Target: `main`
- PR: <URL, if available>

## Summary
<Why the task was done and its result>

## Key changes
- <Important change>

## Code highlights

### `<path/to/file>`
```language
<representative snippet copied from the final diff>
```

## Verification
- <Command/check>: <result>
~~~~

Add the memory file to the same PR branch, but stage only the task's intended files and this new entry. Preserve unrelated existing changes. Never copy secrets, tokens, private credentials, or unrelated personal data into the archive. If the PR is not actually created, do not describe it as an opened PR; report the publishing failure and leave the archive status clear.

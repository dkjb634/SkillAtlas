# Support starring skills

- Date (UTC): 2026-10-01T08:41:11Z
- Branch: `fix/support-starring-skills`
- Target: `main`
- PR: https://github.com/dkjb634/SkillAtlas/pull/8

## Summary
Implemented issue #7 by adding browser-persistent starring for skills and a starred-skills widget.

## Key changes
- Added a star/unstar control to every skill row.
- Persisted stable skill keys in localStorage and rendered starred skills as file links.
- Added a frontend contract regression test.

## Code highlights

### `SkillsAtlas.Web/wwwroot/app.js`
```javascript
function skillKey(skill) {
  return `${skill.repositoryUrl}|${skill.relativeFilePath}|${skill.commitHash}`;
}
```

### `SkillsAtlas.Web/wwwroot/index.html`
```html
<section id="starred-widget" class="starred-widget" aria-labelledby="starred-title" hidden>
  <div id="starred-list" class="starred-list"></div>
</section>
```

## Verification
- `node --check SkillsAtlas.Web/wwwroot/app.js`: passed
- `git diff --check`: passed
- `dotnet test SkillsAtlas.Tests/SkillsAtlas.Tests.csproj --no-restore`: could not run because `dotnet` is not installed in the environment

Produced by Air Automations. Name: Fix Bug / Run: https://air.jetbrains.cloud/org/05cf1a7f-6ab5-713b-abd3-29d0c8a05e2d/automations/5febbd55-48a1-43e6-a21d-dfb8c88bd7d7?run=2e8d6c33-a063-49c6-b1eb-6fceed7abeb7

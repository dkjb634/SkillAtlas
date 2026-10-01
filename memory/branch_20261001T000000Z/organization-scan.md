# Support organization scan

- Date (UTC): 2026-10-01
- Branch: `feature/organization-scan`
- Target: `main`
- PR: pending

## Summary
Implemented issue #9 by adding an Organisation Scan view and a GitHub organization repository discovery endpoint. The scan enumerates accessible repositories, skips archived repositories, scans each repository, and refreshes the saved skill library.

## Key changes
- Added paginated GitHub organization repository discovery with organization URL validation.
- Added `POST /api/organizations/scan` and the corresponding UI workflow.
- Added focused unit coverage for URL validation, pagination, and archived repository filtering.

## Code highlights

### `SkillsAtlas.Core/OrganizationRepositoryResolver.cs`
```csharp
var pageRepositories = await response.Content.ReadFromJsonAsync<IReadOnlyList<GitHubRepository>>(cancellationToken: cancellationToken) ?? [];
repositories.AddRange(pageRepositories
    .Where(repository => !repository.Archived && !string.IsNullOrWhiteSpace(repository.CloneUrl))
    .Select(repository => repository.CloneUrl!));
```

### `SkillsAtlas.Web/Program.cs`
```csharp
var repositories = await resolver.ResolveAsync(request.OrganizationUrl, cancellationToken);
foreach (var repository in repositories)
    await catalog.FetchAsync(repository);
```

## Verification
- `node --check SkillsAtlas.Web/wwwroot/app.js`: passed.
- `git diff --check`: passed.
- `dotnet test SkillsAtlas.Tests/SkillsAtlas.Tests.csproj`: not run; the environment has no `dotnet` executable.

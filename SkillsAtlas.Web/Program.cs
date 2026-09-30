using SkillsAtlas;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(new SkillsCatalog());

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/skills", async (SkillsCatalog catalog) =>
{
    var library = await catalog.GetStoredSkillsAsync();
    return Results.Ok(new
    {
        skills = library.Skills.Select(SkillMarkdownRenderer.Map),
        library.DatabasePath
    });
});

app.MapGet("/api/similar-skills", async (SkillsCatalog catalog) =>
{
    var result = await catalog.GetSimilarSkillsAsync();
    return Results.Ok(result);
});

app.MapPost("/api/skills", async (ScanRequest request, SkillsCatalog catalog) =>
{
    var repositories = (request.Repositories ??
            (string.IsNullOrWhiteSpace(request.Repository) ? [] : [request.Repository]))
        .Select(repository => repository?.Trim())
        .Where(repository => !string.IsNullOrWhiteSpace(repository))
        .Cast<string>()
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
    if (repositories.Length == 0 || repositories.Any(repository => !IsRemoteRepositoryUrl(repository)))
        return Results.BadRequest(new { message = "Enter an HTTP, HTTPS, SSH, or Git repository URL." });

    try
    {
        foreach (var repository in repositories)
            await catalog.FetchAsync(repository);

        var library = await catalog.GetStoredSkillsAsync();
        return Results.Ok(new
        {
            skills = library.Skills.Select(SkillMarkdownRenderer.Map),
            library.DatabasePath,
            searchedRepository = repositories.Length == 1 ? repositories[0] : null,
            searchedRepositories = repositories
        });
    }
    catch (InvalidOperationException exception)
    {
        return Results.UnprocessableEntity(new { message = exception.Message });
    }
});

app.MapFallbackToFile("index.html");
app.Run();

static bool IsRemoteRepositoryUrl(string value)
{
    if (value.StartsWith("git@", StringComparison.Ordinal))
        return true;

    return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
           uri.Scheme is "http" or "https" or "ssh" or "git";
}

internal sealed record ScanRequest(string? Repository, IReadOnlyList<string?>? Repositories);

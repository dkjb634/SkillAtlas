using SkillsAtlas;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(new SkillsCatalog());

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/skills", async (SkillsCatalog catalog) =>
{
    var library = await catalog.GetStoredSkillsAsync();
    return Results.Ok(library);
});

app.MapPost("/api/skills", async (ScanRequest request, SkillsCatalog catalog) =>
{
    var repository = request.Repository?.Trim();
    if (string.IsNullOrWhiteSpace(repository) || !IsRemoteRepositoryUrl(repository))
        return Results.BadRequest(new { message = "Enter an HTTP, HTTPS, SSH, or Git repository URL." });

    try
    {
        await catalog.FetchAsync(repository);
        var library = await catalog.GetStoredSkillsAsync();
        return Results.Ok(new
        {
            skills = library.Skills,
            library.DatabasePath,
            searchedRepository = repository
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

internal sealed record ScanRequest(string? Repository);

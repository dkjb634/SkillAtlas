using System.Net.Http.Headers;
using System.Text.Json;
using SkillsAtlas;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(new SkillsCatalog());
var githubToken = builder.Configuration["GITHUB_TOKEN"];
builder.Services.AddHttpClient<OrganizationRepositoryResolver>(client =>
{
    if (!string.IsNullOrWhiteSpace(githubToken))
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", githubToken.Trim());
});

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

app.MapPost("/api/organizations/scan", async (OrganizationScanRequest request, SkillsCatalog catalog,
    OrganizationRepositoryResolver resolver, HttpContext context, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.OrganizationUrl))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { message = "Enter an organization URL." }, cancellationToken);
        return;
    }

    IReadOnlyList<string> repositories;
    try
    {
        repositories = await resolver.ResolveAsync(request.OrganizationUrl, cancellationToken);
    }
    catch (InvalidOperationException exception)
    {
        context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        await context.Response.WriteAsJsonAsync(new { message = exception.Message }, cancellationToken);
        return;
    }

    context.Response.StatusCode = StatusCodes.Status200OK;
    context.Response.ContentType = "application/x-ndjson; charset=utf-8";
    context.Response.Headers.CacheControl = "no-cache";
    var total = repositories.Count;
    await WriteOrganizationEventAsync(context.Response, new
    {
        type = "start",
        totalRepositories = total,
        organizationUrl = request.OrganizationUrl.Trim()
    }, cancellationToken);

    var completed = 0;
    var failed = 0;
    var discoveredSkills = 0;
    foreach (var repository in repositories)
    {
        IReadOnlyList<SkillMarkdownView> scannedSkills = [];
        string? error = null;
        try
        {
            var result = await catalog.FetchAsync(repository);
            scannedSkills = result.Skills.Select(SkillMarkdownRenderer.Map).ToArray();
            discoveredSkills += scannedSkills.Count;
        }
        catch (InvalidOperationException exception)
        {
            failed++;
            error = exception.Message;
        }

        completed++;
        await WriteOrganizationEventAsync(context.Response, new
        {
            type = "repository",
            repository,
            completedRepositories = completed,
            totalRepositories = total,
            skills = scannedSkills,
            error
        }, cancellationToken);
    }

    await WriteOrganizationEventAsync(context.Response, new
    {
        type = "complete",
        completedRepositories = completed,
        totalRepositories = total,
        failedRepositories = failed,
        discoveredSkills
    }, cancellationToken);
});

app.MapFallbackToFile("index.html");
app.Run();

static async Task WriteOrganizationEventAsync(HttpResponse response, object progressEvent, CancellationToken cancellationToken)
{
    var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    await response.WriteAsync(JsonSerializer.Serialize(progressEvent, progressEvent.GetType(), options) + "\n", cancellationToken);
    await response.Body.FlushAsync(cancellationToken);
}

static bool IsRemoteRepositoryUrl(string value)
{
    if (value.StartsWith("git@", StringComparison.Ordinal))
        return true;

    return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
           uri.Scheme is "http" or "https" or "ssh" or "git";
}

internal sealed record ScanRequest(string? Repository, IReadOnlyList<string?>? Repositories);
internal sealed record OrganizationScanRequest(string? OrganizationUrl);

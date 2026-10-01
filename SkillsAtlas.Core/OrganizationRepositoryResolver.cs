using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace SkillsAtlas;

public sealed class OrganizationRepositoryResolver
{
    private readonly HttpClient _httpClient;

    public OrganizationRepositoryResolver(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SkillsAtlas/1.0");
    }

    public async Task<IReadOnlyList<string>> ResolveAsync(string organizationUrl, CancellationToken cancellationToken = default)
    {
        if (!TryGetGitHubOrganization(organizationUrl, out var organization))
            throw new InvalidOperationException("Enter a GitHub organization URL, such as https://github.com/dotnet.");

        var repositories = new List<string>();
        for (var page = 1; ; page++)
        {
            var url = $"https://api.github.com/orgs/{Uri.EscapeDataString(organization)}/repos?type=all&per_page=100&page={page}";
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden &&
                    response.Headers.TryGetValues("X-RateLimit-Remaining", out var remainingValues) &&
                    remainingValues.FirstOrDefault() == "0")
                    throw new InvalidOperationException($"GitHub's API rate limit was reached while reading organization '{organization}'. Set GITHUB_TOKEN for the SkillsAtlas server and restart it.");

                throw new InvalidOperationException($"GitHub could not read organization '{organization}' ({(int)response.StatusCode}).");
            }

            var pageRepositories = await response.Content.ReadFromJsonAsync<IReadOnlyList<GitHubRepository>>(cancellationToken: cancellationToken) ?? [];
            repositories.AddRange(pageRepositories
                .Where(repository => !repository.Archived && !string.IsNullOrWhiteSpace(repository.CloneUrl))
                .Select(repository => repository.CloneUrl!));
            if (pageRepositories.Count < 100)
                break;
        }

        return repositories;
    }

    internal static bool TryGetGitHubOrganization(string value, out string organization)
    {
        organization = string.Empty;
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
            return false;

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 1 || segments[0].Equals("orgs", StringComparison.OrdinalIgnoreCase))
            return false;

        organization = segments[0];
        return true;
    }

    private sealed record GitHubRepository(
        [property: JsonPropertyName("clone_url")] string? CloneUrl,
        [property: JsonPropertyName("archived")] bool Archived);
}

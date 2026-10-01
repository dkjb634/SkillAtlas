using System.Net;
using System.Text;
using System.Text.Json;
using SkillsAtlas;
using Xunit;

namespace SkillsAtlas.Tests;

public sealed class OrganizationRepositoryResolverTests
{
    [Theory]
    [InlineData("https://github.com/dotnet", "dotnet")]
    [InlineData("https://github.com/dotnet/", "dotnet")]
    public void TryGetGitHubOrganization_ExtractsOrganizationName(string url, string expected)
    {
        Assert.True(OrganizationRepositoryResolver.TryGetGitHubOrganization(url, out var organization));
        Assert.Equal(expected, organization);
    }

    [Theory]
    [InlineData("https://github.com/dotnet/runtime")]
    [InlineData("https://gitlab.com/dotnet")]
    public void TryGetGitHubOrganization_RejectsNonOrganizationUrls(string url)
    {
        Assert.False(OrganizationRepositoryResolver.TryGetGitHubOrganization(url, out _));
    }

    [Fact]
    public async Task ResolveAsync_ReturnsNonArchivedRepositoriesAndRequestsAllPages()
    {
        var handler = new StubHandler(request =>
        {
            var page = request.RequestUri!.Query.Contains("page=1") ?
                Enumerable.Range(0, 99)
                    .Select(_ => new { clone_url = "https://github.com/acme/archived.git", archived = true })
                    .Append(new { clone_url = "https://github.com/acme/one.git", archived = false })
                    .ToArray() :
                [new { clone_url = "https://github.com/acme/two.git", archived = false }];
            var json = JsonSerializer.Serialize(page);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        });
        using var client = new HttpClient(handler);
        var resolver = new OrganizationRepositoryResolver(client);

        var repositories = await resolver.ResolveAsync("https://github.com/acme");

        Assert.Equal(["https://github.com/acme/one.git", "https://github.com/acme/two.git"], repositories);
        Assert.Equal(2, handler.Requests.Count);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(responseFactory(request));
        }
    }
}
